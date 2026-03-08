using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.Enterprise.ServerValidation;

/// <summary>
/// Rotates the server nonce after successful chain submission to prevent replay attacks.
/// </summary>
public sealed class NonceRotator(
    LicenseConfigs configs,
    ChainSubmitter submitter,
    LicenseStore store,
    IMLog<NonceRotator>? logger = null)
{
    public async Task RotateAsync(IEnumerable<FingerprintChainEntry> entries, string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedTenant = AuditTrailTenantPartition.Normalize(tenantId);
        long entryCount = entries.LongCount();
        string status = "ok";
        Stopwatch sw = Stopwatch.StartNew();

        using Activity? activity = AuditTrailRuntimeTelemetry.ActivitySource.StartActivity(
            "audit-trail.rotate_nonce",
            ActivityKind.Internal);
        activity?.SetTag("audittrail.operation", "rotate_nonce");
        activity?.SetTag("tenant.id", normalizedTenant);
        activity?.SetTag("audittrail.entry_count", entryCount);

        logger?.LogInformation("[License] Starting action chain submission and nonce rotation...");
        try
        {
            ChainSubmissionResponse response = await submitter.SubmitAsync(entries, tenantId, cancellationToken);

            if (response.Accepted && !string.IsNullOrWhiteSpace(response.NewNonce))
            {
                UpdateLocalNonce(response.NewNonce);
                logger?.LogInformation("[License] Nonce rotated successfully. New nonce received from server.");
            }
            else if (!response.Accepted)
            {
                status = "error";
                activity?.SetStatus(ActivityStatusCode.Error, response.Error ?? "nonce-rotation-rejected");
                logger?.Warn("[License] Nonce rotation failed: {Error}", response.Error);
            }
        }
        catch (Exception ex)
        {
            status = "error";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger?.LogError(ex, "[License] Error rotating nonce.");
            throw;
        }
        finally
        {
            sw.Stop();
            AuditTrailRuntimeTelemetry.TrackOperation(
                operation: "rotate_nonce",
                status: status,
                tenantId: normalizedTenant,
                chainStorage: configs.ChainStorage.ToString(),
                elapsed: sw.Elapsed,
                entryCount: entryCount);
        }
    }

    private void UpdateLocalNonce(string newNonce)
    {
        LicensePayload? payload = store.Load();
        if (payload == null)
        {
            return;
        }

        payload.ServerNonce = newNonce;
        store.Save(payload);
    }
}
