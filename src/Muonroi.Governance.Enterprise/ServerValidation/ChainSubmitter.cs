namespace Muonroi.Governance.Enterprise.ServerValidation;

/// <summary>
/// Handles the submission of action chains to the license server for audit and verification.
/// </summary>
public sealed class ChainSubmitter(
    LicenseConfigs configs,
    IHttpClientFactory httpClientFactory,
    IMLog<ChainSubmitter>? logger = null,
    LicenseState? licenseState = null,
    IServiceScopeFactory? scopeFactory = null,
    IFailedChainSubmissionStore? failedStore = null)
{
    private readonly LicenseState _licenseState = licenseState ?? LicenseState.CreateFree();

    /// <summary>
    /// Submits an action chain to the license server. On transient failure (network error / 5xx)
    /// the batch is persisted to the failed-submission store (when configured) for later retry via
    /// <see cref="RetryPendingAsync"/>, so the audit trail is not silently lost.
    /// </summary>
    public Task<ChainSubmissionResponse> SubmitAsync(IEnumerable<FingerprintChainEntry> entries, string? tenantId,
        CancellationToken cancellationToken = default)
        => SubmitInternalAsync(entries, tenantId, persistOnFailure: true, cancellationToken);

    private async Task<ChainSubmissionResponse> SubmitInternalAsync(IEnumerable<FingerprintChainEntry> entries, string? tenantId,
        bool persistOnFailure, CancellationToken cancellationToken = default)
    {
        List<FingerprintChainEntry> normalizedEntries = NormalizeEntries(entries);
        if (!TryResolveTenantPartition(tenantId, normalizedEntries, out string? normalizedTenant, out string? tenantError))
        {
            Stopwatch tenantErrorSw = Stopwatch.StartNew();
            using Activity? tenantErrorActivity = AuditTrailRuntimeTelemetry.ActivitySource.StartActivity(
                "audit-trail.submit_chain",
                ActivityKind.Client);
            tenantErrorActivity?.SetTag("audittrail.operation", "submit_chain");
            tenantErrorActivity?.SetTag("tenant.id", normalizedTenant);
            tenantErrorActivity?.SetStatus(ActivityStatusCode.Error, tenantError);
            tenantErrorSw.Stop();
            AuditTrailRuntimeTelemetry.TrackOperation(
                operation: "submit_chain",
                status: "error",
                tenantId: normalizedTenant,
                chainStorage: configs.ChainStorage.ToString(),
                elapsed: tenantErrorSw.Elapsed,
                entryCount: normalizedEntries.Count);
            ChainSubmissionResponse async = new()
            {
                Accepted = false,
                Error = tenantError
            };
            return async;
        }

        string status = "ok";
        Stopwatch sw = Stopwatch.StartNew();
        int entryCount = normalizedEntries.Count;

        using Activity? activity = AuditTrailRuntimeTelemetry.ActivitySource.StartActivity(
            "audit-trail.submit_chain",
            ActivityKind.Client);
        activity?.SetTag("audittrail.operation", "submit_chain");
        activity?.SetTag("tenant.id", normalizedTenant);
        activity?.SetTag("audittrail.entry_count", entryCount);

        if (entryCount == 0)
        {
            sw.Stop();
            AuditTrailRuntimeTelemetry.TrackOperation(
                operation: "submit_chain",
                status: status,
                tenantId: normalizedTenant,
                chainStorage: configs.ChainStorage.ToString(),
                elapsed: sw.Elapsed,
                entryCount: 0);
            ChainSubmissionResponse async = new()
            {
                Accepted = true
            };
            return async;
        }

        if (!EnsureEnterpriseRemoteTrustRequirements(out string? trustError))
        {
            status = "error";
            activity?.SetStatus(ActivityStatusCode.Error, trustError);
            sw.Stop();
            AuditTrailRuntimeTelemetry.TrackOperation(
                operation: "submit_chain",
                status: status,
                tenantId: normalizedTenant,
                chainStorage: configs.ChainStorage.ToString(),
                elapsed: sw.Elapsed,
                entryCount: entryCount);
            ChainSubmissionResponse async = new()
            {
                Accepted = false,
                Error = trustError
            };
            return async;
        }

        // TIER 3+: Verify endpoint domain to prevent DNS hijacking
        if (!VerifyEndpointDomain())
        {
            logger?.LogError("[SECURITY ALERT] Endpoint domain verification FAILED! Refusing to connect.");
            status = "error";
            activity?.SetStatus(ActivityStatusCode.Error, "endpoint-domain-verification-failed");
            sw.Stop();
            AuditTrailRuntimeTelemetry.TrackOperation(
                operation: "submit_chain",
                status: status,
                tenantId: normalizedTenant,
                chainStorage: configs.ChainStorage.ToString(),
                elapsed: sw.Elapsed,
                entryCount: entryCount);
            ChainSubmissionResponse async = new()
            {
                Accepted = false,
                Error = "Invalid endpoint domain - possible DNS hijacking"
            };
            return async;
        }

        ChainSubmissionRequest request = new()
        {
            LicenseId = configs.LicenseFilePath ?? "UNKNOWN", // Simplified for now
            TenantId = normalizedTenant,
            Entries = normalizedEntries,
            SubmittedAt = DateTimeOffset.UtcNow
        };

        try
        {
            EnsureAuditTrailLicensed();

            HttpClient client = httpClientFactory.CreateClient("LicenseServer");
            string endpoint = configs.Online.ChainSubmissionEndpoint ?? "/api/v1/chain/submit";

            HttpResponseMessage response = await client.PostAsJsonAsync(endpoint, request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                ChainSubmissionResponse? result = await response.Content.ReadFromJsonAsync<ChainSubmissionResponse>(cancellationToken: cancellationToken);
                if (result == null)
                {
                    ChainSubmissionResponse async = new()
                    {
                        Accepted = false,
                        Error = "Empty response from server."
                    };
                    return async;
                }

                // TIER 3+: Verify response signature to prevent fake server responses
                if (!VerifyResponseSignature(result, request.LicenseId))
                {
                    logger?.LogError("[SECURITY ALERT] Server response signature verification FAILED! Possible fake server.");
                    status = "error";
                    activity?.SetStatus(ActivityStatusCode.Error, "invalid-server-signature");
                    ChainSubmissionResponse async = new()
                    {
                        Accepted = false,
                        Error = "Invalid server signature - refusing response"
                    };
                    return async;
                }

                return result;
            }

            string error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger?.Warn("[License] Chain submission failed: {Status}. {Error}", response.StatusCode, error);
            status = "error";
            activity?.SetStatus(ActivityStatusCode.Error, $"server-status:{response.StatusCode}");

            // 5xx is a transient server-side fault — persist for retry so the audit chain is not lost.
            // 4xx is a deterministic rejection (bad request / unauthorized) and is NOT enqueued.
            if (persistOnFailure && (int)response.StatusCode >= 500)
            {
                await TryEnqueueFailedAsync(request, $"Server returned {response.StatusCode}", cancellationToken);
            }

            ChainSubmissionResponse submitAsync = new()
            {
                Accepted = false,
                Error = $"Server returned {response.StatusCode}: {error}"
            };
            return submitAsync;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[License] Error submitting action chain to server.");
            status = "error";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Network/timeout faults are transient — persist for retry. License/validation
            // exceptions are deterministic and are NOT enqueued (they would never succeed).
            if (persistOnFailure && IsTransient(ex, cancellationToken))
            {
                await TryEnqueueFailedAsync(request, ex.Message, cancellationToken);
            }

            ChainSubmissionResponse async = new()
            {
                Accepted = false,
                Error = ex.Message
            };
            return async;
        }
        finally
        {
            sw.Stop();
            AuditTrailRuntimeTelemetry.TrackOperation(
                operation: "submit_chain",
                status: status,
                tenantId: normalizedTenant,
                chainStorage: configs.ChainStorage.ToString(),
                elapsed: sw.Elapsed,
                entryCount: entryCount);
        }
    }

    /// <summary>
    /// Verifies the RSA signature on a server response to prevent fake server attacks.
    /// </summary>
    private bool VerifyResponseSignature(ChainSubmissionResponse response, string licenseId)
    {
        bool requireSignature = MEnterpriseSecurityProfile.RequiresServerResponseSignature(configs, _licenseState);

        // Skip verification if signature is missing (for backward compatibility with old servers)
        if (string.IsNullOrEmpty(response.Signature))
        {
            if (requireSignature)
            {
                logger?.LogError("[SECURITY ALERT] Server response signature is required in Enterprise Production.");
                return false;
            }

            logger?.Warn("[Security] Server response missing signature - consider enabling RequireServerSignature in production");
            return true; // Allow for now (backward compatibility)
        }

        try
        {
            // Reconstruct the data that was signed by the server
            // Format: "Accepted|NewNonce|LicenseId"
            string signingData = $"{response.Accepted}|{response.NewNonce ?? ""}|{licenseId}";
            byte[] dataBytes = Encoding.UTF8.GetBytes(signingData);
            byte[] signatureBytes = Convert.FromBase64String(response.Signature);

            // Load the public key (same key used for license verification)
            using RSA? rsa = LoadPublicKey();
            if (rsa == null)
            {
                if (requireSignature)
                {
                    logger?.LogError("[SECURITY ALERT] Public key unavailable while response signature is required.");
                    return false;
                }

                logger?.Warn("[Security] Cannot verify response signature - public key not available");
                return true; // Allow if public key not configured (offline mode)
            }

            // Verify the signature
            bool isValid = rsa.VerifyData(
                dataBytes,
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            );

            if (!isValid)
            {
                logger?.LogError("[SECURITY ALERT] Response signature verification failed!");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[Security] Error verifying response signature");
            return false; // Fail-safe: reject on error
        }
    }

    /// <summary>
    /// Loads the RSA public key from configuration or embedded resource.
    /// Uses the same public key as license verification.
    /// </summary>
    private RSA? LoadPublicKey()
    {
        try
        {
            // If PublicKeyPath is configured, load from file using MSecureFileReader for path traversal safety
            if (!string.IsNullOrEmpty(configs.PublicKeyPath))
            {
                string pemContent = MSecureFileReader.ReadKeyFile(configs.PublicKeyPath);
                RSA rsa = RSA.Create();
                rsa.ImportFromPem(pemContent.ToCharArray());
                return rsa;
            }

            // Otherwise, try to load from embedded resource (default public key)
            Assembly assembly = typeof(ChainSubmitter).Assembly;
            string resourceName = "Muonroi.BuildingBlock.Resources.public_key.pem";
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using StreamReader reader = new(stream);
                string pemContent = reader.ReadToEnd();
                RSA rsa = RSA.Create();
                rsa.ImportFromPem(pemContent.ToCharArray());
                return rsa;
            }

            return null;
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "[Security] Failed to load public key for response verification");
            return null;
        }
    }

    /// <summary>
    /// TIER 3+: Verifies that the endpoint domain is trusted to prevent DNS hijacking attacks.
    /// Returns true if the domain is trusted, false otherwise.
    /// </summary>
    private bool VerifyEndpointDomain()
    {
        try
        {
            HttpClient client = httpClientFactory.CreateClient("LicenseServer");
            string? actualEndpoint = client.BaseAddress?.Host;

            if (string.IsNullOrEmpty(actualEndpoint))
            {
                logger?.Warn("[Security] Cannot verify endpoint - BaseAddress is null");
                return !MEnterpriseSecurityProfile.RequiresTrustedEndpoint(configs, _licenseState);
            }

            // Allow localhost ONLY in Development environment
            if (actualEndpoint == "localhost" || actualEndpoint == "127.0.0.1" || actualEndpoint.StartsWith("192.168."))
            {
                string? env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                bool isDevelopment = env?.Equals("Development", StringComparison.OrdinalIgnoreCase) == true;

                if (!isDevelopment)
                {
                    logger?.LogError("[SECURITY ALERT] Localhost/private IP endpoint detected in non-Development environment!");
                }

                return isDevelopment;
            }

            // Check against trusted domains
            bool isTrusted = MEnterpriseSecurityProfile.IsTrustedLicenseServerHost(configs, actualEndpoint);

            if (!isTrusted)
            {
                logger?.LogError("[SECURITY ALERT] Untrusted endpoint domain: {Domain}", actualEndpoint);
            }

            return isTrusted;
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "[Security] Error verifying endpoint domain");
            return !MEnterpriseSecurityProfile.RequiresTrustedEndpoint(configs, _licenseState);
        }
    }

    private void EnsureAuditTrailLicensed()
    {
        if (scopeFactory is not null)
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ILicenseGuard? licenseGuard = scope.ServiceProvider.GetService<ILicenseGuard>();
            if (licenseGuard is not null)
            {
                licenseGuard.EnsureFeature(FreeTierFeatures.Premium.AuditTrail);
                return;
            }
        }

        MGuard.State(_licenseState.HasFeature(FreeTierFeatures.Premium.AuditTrail),
            "[LICENSE] Feature 'audit-trail' is not available under your current license.");
    }

    private static List<FingerprintChainEntry> NormalizeEntries(IEnumerable<FingerprintChainEntry>? entries)
    {
        if (entries is null)
        {
            return [];
        }

        return [.. entries
            .Select(entry =>
            {
                FingerprintChainEntry chainEntry = new() {
                    Sequence = entry.Sequence,
                    Timestamp = entry.Timestamp,
                    TenantId = AuditTrailTenantPartition.Normalize(entry.TenantId),
                    ActionType = entry.ActionType,
                    ActionName = entry.ActionName,
                    PayloadHash = entry.PayloadHash,
                    PreviousSignature = entry.PreviousSignature,
                    Signature = entry.Signature
                };
                return chainEntry;
            })];
    }

    private static bool TryResolveTenantPartition(
        string? tenantId,
        List<FingerprintChainEntry> entries,
        out string normalizedTenant,
        out string? error)
    {
        error = null;
        normalizedTenant = AuditTrailTenantPartition.Normalize(tenantId);

        if (entries.Count == 0)
        {
            return true;
        }

        List<string> entryPartitions = [.. entries
            .Select(entry => AuditTrailTenantPartition.Normalize(entry.TenantId))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            if (entryPartitions.Count > 1)
            {
                error = "Entries contain mixed tenant partitions; submit per tenant.";
                return false;
            }

            normalizedTenant = entryPartitions[0];
        }
        else
        {
            string expectedTenant = normalizedTenant;
            if (entryPartitions.Any(partition =>
                    !string.Equals(partition, expectedTenant, StringComparison.OrdinalIgnoreCase)))
            {
                error = "Entries do not match requested tenant partition.";
                return false;
            }
        }

        foreach (FingerprintChainEntry entry in entries)
        {
            entry.TenantId = normalizedTenant;
        }

        return true;
    }

    private bool EnsureEnterpriseRemoteTrustRequirements(out string? error)
    {
        error = null;
        if (!MEnterpriseSecurityProfile.IsEndpointTrustFailClosed(configs, _licenseState))
        {
            return true;
        }

        if (MEnterpriseSecurityProfile.RequiresCertificatePinning(configs, _licenseState))
        {
            if (!configs.Online.EnableCertificatePinning)
            {
                error = "[SEC_FAIL_CLOSED] Enterprise Production requires certificate pinning.";
                return false;
            }

            if (!MEnterpriseSecurityProfile.HasPinningMaterial(configs))
            {
                error = "[SEC_FAIL_CLOSED] Certificate pinning requires ExpectedCertificateThumbprint or TrustedCertificateThumbprints.";
                return false;
            }
        }

        if (!MEnterpriseSecurityProfile.RequiresTrustedEndpoint(configs, _licenseState) ||
            VerifyEndpointDomain())
        {
            return true;
        }

        error = "[SEC_FAIL_CLOSED] Enterprise Production requires a trusted license server endpoint.";
        return false;

    }

    /// <summary>
    /// Re-submits all pending (previously-failed) audit chain submissions. Accepted batches are
    /// removed from the queue; batches that exhaust <paramref name="maxAttempts"/> are dead-lettered
    /// (removed and logged) so the queue cannot grow unbounded. No-op when no failed-submission
    /// store is configured. Intended to be called on a schedule or at startup by the host.
    /// </summary>
    public async Task<ChainRetryResult> RetryPendingAsync(int maxAttempts = 5, CancellationToken cancellationToken = default)
    {
        ChainRetryResult result = new();
        if (failedStore is null)
        {
            return result;
        }

        IReadOnlyList<PendingChainSubmission> pendings = await failedStore.ListPendingAsync(cancellationToken);
        foreach (PendingChainSubmission pending in pendings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // persistOnFailure:false — this method owns the pending lifecycle; do not double-enqueue.
            ChainSubmissionResponse response = await SubmitInternalAsync(
                pending.Request.Entries, pending.Request.TenantId, persistOnFailure: false, cancellationToken);

            if (response.Accepted)
            {
                await failedStore.RemoveAsync(pending.Id, cancellationToken);
                result.Succeeded++;
                continue;
            }

            pending.AttemptCount++;
            pending.LastAttemptUtc = DateTimeOffset.UtcNow;
            pending.LastError = response.Error;

            if (pending.AttemptCount >= maxAttempts)
            {
                await failedStore.RemoveAsync(pending.Id, cancellationToken);
                result.Dropped++;
                logger?.LogError(
                    "[License] Dead-lettering audit chain submission '{Id}' after {Attempts} attempts. Last error: {Error}",
                    pending.Id, pending.AttemptCount, pending.LastError);
            }
            else
            {
                await failedStore.UpdateAsync(pending, cancellationToken);
                result.StillPending++;
            }
        }

        return result;
    }

    private async Task TryEnqueueFailedAsync(ChainSubmissionRequest request, string error, CancellationToken cancellationToken)
    {
        if (failedStore is null)
        {
            return;
        }

        try
        {
            PendingChainSubmission pending = new()
            {
                Id = Guid.NewGuid().ToString("N"),
                Request = request,
                FirstFailedAtUtc = DateTimeOffset.UtcNow,
                LastAttemptUtc = DateTimeOffset.UtcNow,
                AttemptCount = 1,
                LastError = error
            };
            await failedStore.EnqueueAsync(pending, cancellationToken);
            logger?.Warn("[License] Audit chain submission persisted for retry ({Count} entries). Reason: {Error}",
                request.Entries.Count, error);
        }
        catch (Exception ex)
        {
            // Best-effort durability — never let persistence failure mask the original submit outcome.
            logger?.LogError(ex, "[License] Failed to persist audit chain submission for retry.");
        }
    }

    private static bool IsTransient(Exception ex, CancellationToken cancellationToken)
    {
        // Caller-initiated cancellation is not a transient failure.
        if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return ex is HttpRequestException
            || ex is TaskCanceledException // HTTP timeout (caller-cancellation handled above)
            || ex is IOException
            || ex.InnerException is System.Net.Sockets.SocketException;
    }
}

/// <summary>
/// Outcome of draining the pending audit-chain submission queue via
/// <see cref="ChainSubmitter.RetryPendingAsync"/>.
/// </summary>
public sealed class ChainRetryResult
{
    /// <summary>Submissions accepted by the server and removed from the queue.</summary>
    public int Succeeded { get; set; }
    /// <summary>Submissions that exhausted retries and were dead-lettered (removed + logged).</summary>
    public int Dropped { get; set; }
    /// <summary>Submissions that failed again but remain queued for a future attempt.</summary>
    public int StillPending { get; set; }
}

/// <summary>
/// Represents the Chain Submission Request.
/// </summary>
public sealed class ChainSubmissionRequest
{
    /// <summary>
    /// Gets or sets the License Id.
    /// </summary>
    public string LicenseId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// Gets or sets the Entries.
    /// </summary>
    public List<FingerprintChainEntry> Entries { get; set; } = [];
    /// <summary>
    /// Gets or sets the Submitted At.
    /// </summary>
    public DateTimeOffset SubmittedAt { get; set; }
}

/// <summary>
/// Represents the Chain Submission Response.
/// </summary>
public sealed class ChainSubmissionResponse
{
    /// <summary>
    /// Gets or sets the Accepted.
    /// </summary>
    public bool Accepted { get; set; }
    /// <summary>
    /// Gets or sets the New Nonce.
    /// </summary>
    public string? NewNonce { get; set; }
    /// <summary>
    /// Gets or sets the Error.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// RSA-SHA256 signature of the response, signed by the license server.
    /// Used to verify the response is authentic and prevent fake server attacks.
    /// </summary>
    public string? Signature { get; set; }
}
