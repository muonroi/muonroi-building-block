namespace Muonroi.Governance.Tests;

[Collection("NonParallel")]
public class AuditTrailChainSubmitterTests
{
    private static readonly LicenseState AuditTrailLicensed = new()
    {
        IsValid = true,
        Tier = LicenseTier.Licensed,
        Features = [FreeTierFeatures.Premium.AuditTrail]
    };

    private sealed class DenyAuditTrailGuard : ILicenseGuard
    {
        public LicenseState Current => AuditTrailLicensed;
        public LicenseTier Tier => LicenseTier.Licensed;
        public bool IsFreeMode => false;

        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
            string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName)
        {
            return !string.Equals(featureName, FreeTierFeatures.Premium.AuditTrail, StringComparison.OrdinalIgnoreCase);
        }

        public void EnsureFeature(string featureName)
        {
            if (!HasFeature(featureName))
            {
                throw new InvalidOperationException("audit-trail feature blocked by guard");
            }
        }

        public void RecordAction(LicenseActionContext context)
        {
        }

        public string GetChainToken() => string.Empty;

        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
        {
            return encryptedData;
        }
    }

    [Fact]
    public async Task SubmitAsync_WithTenantId_SendsTenantPartition()
    {
        string? capturedBody = null;
        TestHandler handler = new((request, _) =>
        {
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            HttpResponseMessage message = new(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new ChainSubmissionResponse
                {
                    Accepted = true,
                    NewNonce = "nonce"
                })
            };
            return message;
        });

        LicenseConfigs configs = CreateConfigs("https://license.muonroi.com");
        ChainSubmitter submitter = new(
            configs,
            new TestHttpClientFactory(handler, "https://license.muonroi.com"),
            licenseState: AuditTrailLicensed);

        FingerprintChainEntry entry = new()
        {
            Sequence = 1,
            TenantId = "tenant-a",
            ActionType = "api.request",
            Signature = "sig-1"
        };
        ChainSubmissionResponse response = await submitter.SubmitAsync(
            new[]
            {
                entry
            },
            "tenant-a");

        Assert.True(response.Accepted);
        Assert.NotNull(capturedBody);

        ChainSubmissionRequest? request = JsonSerializer.Deserialize<ChainSubmissionRequest>(
            capturedBody!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(request);
        Assert.Equal("tenant-a", request!.TenantId);
        Assert.Single(request.Entries);
        Assert.Equal("tenant-a", request.Entries[0].TenantId);
    }

    [Fact]
    public async Task SubmitAsync_EmptyEntries_DoesNotCallRemote()
    {
        TestHandler handler = new((_, _) => throw new InvalidOperationException("should not be called"));
        LicenseConfigs configs = CreateConfigs("https://license.muonroi.com");
        ChainSubmitter submitter = new(configs, new TestHttpClientFactory(handler, "https://license.muonroi.com"));

        ChainSubmissionResponse response = await submitter.SubmitAsync([], "tenant-empty");

        Assert.True(response.Accepted);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SubmitAsync_UntrustedEndpoint_Rejects()
    {
        TestHandler handler = new((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        LicenseConfigs configs = CreateConfigs("https://evil-license.example");
        ChainSubmitter submitter = new(
            configs,
            new TestHttpClientFactory(handler, "https://evil-license.example"),
            licenseState: AuditTrailLicensed);

        FingerprintChainEntry entry = new()
        {
            Sequence = 1,
            TenantId = "tenant-x",
            ActionType = "api.request",
            Signature = "sig-x"
        };
        ChainSubmissionResponse response = await submitter.SubmitAsync(
            new[]
            {
                entry
            },
            "tenant-x");

        Assert.False(response.Accepted);
        Assert.Contains("Invalid endpoint domain", response.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitAsync_FreeModeWithEntries_Rejects_ByLicense()
    {
        TestHandler handler = new((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        LicenseConfigs configs = CreateConfigs("https://license.muonroi.com");
        ChainSubmitter submitter = new(configs, new TestHttpClientFactory(handler, "https://license.muonroi.com"));

        FingerprintChainEntry entry = new()
        {
            Sequence = 1,
            TenantId = "tenant-a",
            ActionType = "api.request",
            Signature = "sig"
        };
        ChainSubmissionResponse response = await submitter.SubmitAsync(
            [entry],
            "tenant-a");

        Assert.False(response.Accepted);
        Assert.Contains("audit-trail", response.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SubmitAsync_LicenseGuardDenies_TakesPrecedence()
    {
        TestHandler handler = new((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        LicenseConfigs configs = CreateConfigs("https://license.muonroi.com");

        ServiceCollection services = [];
        services.AddScoped<ILicenseGuard>(_ => new DenyAuditTrailGuard());
        using ServiceProvider provider = services.BuildServiceProvider();

        ChainSubmitter submitter = new(
            configs,
            new TestHttpClientFactory(handler, "https://license.muonroi.com"),
            licenseState: AuditTrailLicensed,
            scopeFactory: provider.GetRequiredService<IServiceScopeFactory>());

        FingerprintChainEntry entry = new()
        {
            Sequence = 1,
            TenantId = "tenant-a",
            ActionType = "api.request",
            Signature = "sig"
        };
        ChainSubmissionResponse response = await submitter.SubmitAsync(
            [entry],
            "tenant-a");

        Assert.False(response.Accepted);
        Assert.Contains("audit-trail feature blocked by guard", response.Error ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SubmitAsync_MixedTenantEntries_Rejects()
    {
        TestHandler handler = new((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        LicenseConfigs configs = CreateConfigs("https://license.muonroi.com");
        ChainSubmitter submitter = new(
            configs,
            new TestHttpClientFactory(handler, "https://license.muonroi.com"),
            licenseState: AuditTrailLicensed);

        FingerprintChainEntry entry = new()
        {
            Sequence = 1,
            TenantId = "tenant-a",
            ActionType = "api.request",
            Signature = "sig-a"
        };
        ChainSubmissionResponse response = await submitter.SubmitAsync(
        [
            entry,
            new FingerprintChainEntry { Sequence = 2, TenantId = "tenant-b", ActionType = "api.request", Signature = "sig-b" }
        ], "tenant-a");

        Assert.False(response.Accepted);
        Assert.Contains("tenant", response.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SubmitAsync_EmitsActivity_WithTenantTag()
    {
        TestHandler handler = new((_, _) =>
        {
            HttpResponseMessage message = new(HttpStatusCode.OK);
            ChainSubmissionResponse value = new()
            {
                Accepted = true,
                NewNonce = "nonce"
            };
            message.Content = System.Net.Http.Json.JsonContent.Create(value);
            return message;
        });
        LicenseConfigs configs = CreateConfigs("https://license.muonroi.com");
        ChainSubmitter submitter = new(
            configs,
            new TestHttpClientFactory(handler, "https://license.muonroi.com"),
            licenseState: AuditTrailLicensed);

        List<Activity> stopped = [];
        using ActivityListener listener = new();
        listener.ShouldListenTo = source => source.Name == AuditTrailRuntimeTelemetry.ActivitySourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = activity => stopped.Add(activity);
        ActivitySource.AddActivityListener(listener);

        FingerprintChainEntry entry = new()
        {
            Sequence = 1,
            TenantId = "tenant-a",
            ActionType = "api.request",
            Signature = "sig"
        };
        ChainSubmissionResponse response = await submitter.SubmitAsync(
            [entry],
            "tenant-a");

        Assert.True(response.Accepted);
        Activity? submitActivity = stopped.LastOrDefault(activity =>
            string.Equals(activity.OperationName, "audit-trail.submit_chain", StringComparison.Ordinal));
        Assert.NotNull(submitActivity);
        Assert.Equal("submit_chain", submitActivity!.GetTagItem("audittrail.operation"));
        Assert.Equal("tenant-a", submitActivity.GetTagItem("tenant.id"));
    }

    [Fact]
    public async Task SubmitAsync_WithoutTenantId_InferTenantFromEntries()
    {
        string? capturedBody = null;
        TestHandler handler = new((request, _) =>
        {
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            HttpResponseMessage message = new(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new ChainSubmissionResponse
                {
                    Accepted = true
                })
            };
            return message;
        });

        LicenseConfigs configs = CreateConfigs("https://license.muonroi.com");
        ChainSubmitter submitter = new(
            configs,
            new TestHttpClientFactory(handler, "https://license.muonroi.com"),
            licenseState: AuditTrailLicensed);

        FingerprintChainEntry chainEntry = new()
        {
            Sequence = 1,
            TenantId = "tenant-inferred",
            ActionType = "api.request",
            Signature = "sig"
        };
        ChainSubmissionResponse response = await submitter.SubmitAsync(
            [chainEntry],
            tenantId: null);

        Assert.True(response.Accepted);
        Assert.NotNull(capturedBody);

        ChainSubmissionRequest? request = JsonSerializer.Deserialize<ChainSubmissionRequest>(
            capturedBody!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(request);
        Assert.Equal("tenant-inferred", request!.TenantId);
        Assert.All(request.Entries, entry => Assert.Equal("tenant-inferred", entry.TenantId));
    }

    [Fact]
    public async Task SubmitAsync_EnterpriseProduction_PinningRequired_FailClosedWhenMissingPinning()
    {
        TestHandler handler = new((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        LicenseState state = new()
        {
            IsValid = true,
            Tier = LicenseTier.Enterprise,
            Payload = new LicensePayload()
        };
        state.Payload.LicenseId = "ENT-SEC-001";
        state.Payload.AllowedFeatures = ["*"];
        LicenseConfigs configs = CreateConfigs("https://license.muonroi.com");
        configs.EnforcementMode = LicenseEnforcementMode.Production;
        configs.Online.EnableCertificatePinning = false;
        configs.Enterprise = new MEnterpriseSecurityConfigs
        {
            EnableSecureDefaults = true,
            RequireCertificatePinningInProduction = true,
            AllowEndpointTrustBypassInProduction = false
        };

        ChainSubmitter submitter = new(
            configs,
            new TestHttpClientFactory(handler, "https://license.muonroi.com"),
            licenseState: state);

        FingerprintChainEntry entry = new()
        {
            Sequence = 1,
            TenantId = "tenant-e2",
            ActionType = "api.request",
            Signature = "sig"
        };
        ChainSubmissionResponse response = await submitter.SubmitAsync(
            [entry],
            "tenant-e2");

        Assert.False(response.Accepted);
        Assert.Contains("certificate pinning", response.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SubmitAsync_EnterpriseProduction_MissingServerSignature_FailClosed()
    {
        TestHandler handler = new((_, _) =>
        {
            HttpResponseMessage message = new(HttpStatusCode.OK);
            ChainSubmissionResponse value = new()
            {
                Accepted = true,
                NewNonce = "nonce-without-signature",
                Signature = null
            };
            message.Content = System.Net.Http.Json.JsonContent.Create(value);
            return message;
        });
        LicenseState state = new()
        {
            IsValid = true,
            Tier = LicenseTier.Enterprise,
            Payload = new LicensePayload()
        };
        state.Payload.LicenseId = "ENT-SEC-002";
        state.Payload.AllowedFeatures = ["*"];
        LicenseConfigs configs = CreateConfigs("https://license.muonroi.com");
        configs.EnforcementMode = LicenseEnforcementMode.Production;
        configs.Online.EnableCertificatePinning = true;
        configs.Online.ExpectedCertificateThumbprint = "AA11BB22CC33DD44EE55FF66778899AA00112233445566778899AABBCCDDEEFF";
        configs.Enterprise = new MEnterpriseSecurityConfigs
        {
            EnableSecureDefaults = true,
            RequireServerResponseSignatureInProduction = true,
            RequireCertificatePinningInProduction = true,
            RequireTrustedEndpointInProduction = true,
            AllowEndpointTrustBypassInProduction = false
        };

        ChainSubmitter submitter = new(
            configs,
            new TestHttpClientFactory(handler, "https://license.muonroi.com"),
            licenseState: state);

        FingerprintChainEntry entry = new()
        {
            Sequence = 1,
            TenantId = "tenant-e2",
            ActionType = "api.request",
            Signature = "sig"
        };
        ChainSubmissionResponse response = await submitter.SubmitAsync(
            [entry],
            "tenant-e2");

        Assert.False(response.Accepted);
        Assert.Contains("Invalid server signature", response.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static LicenseConfigs CreateConfigs(string endpoint)
    {
        LicenseConfigs configs = new()
        {
            ChainStorage = LicenseChainStorage.File,
            Online = new OnlineLicenseConfigs
            {
                Endpoint = endpoint,
                ChainSubmissionEndpoint = "/api/v1/chain/submit"
            }
        };
        return configs;
    }

    private sealed class TestHttpClientFactory(TestHandler handler, string baseAddress) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            HttpClient client = new(handler)
            {
                BaseAddress = new Uri(baseAddress)
            };
            return client;
        }
    }

    private sealed class TestHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responder(request, cancellationToken));
        }
    }
}

