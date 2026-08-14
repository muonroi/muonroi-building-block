namespace Muonroi.Governance.Enterprise.Tests.ServerValidation;

public class ChainSubmitterTests
{
    private readonly LicenseConfigs _configs;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMLog<ChainSubmitter> _logger;

    public ChainSubmitterTests()
    {
        _configs = new LicenseConfigs
        {
            Online = new OnlineLicenseConfigs
            {
                ChainSubmissionEndpoint = "/api/v1/chain/submit"
            },
            Enterprise = new MEnterpriseSecurityConfigs
            {
                EnableSecureDefaults = false // Disable by default for tests
            }
        };


        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _logger = Substitute.For<IMLog<ChainSubmitter>>();
    }

    [Fact]
    public async Task SubmitAsync_WithNoEntries_ShouldReturnAccepted()
    {
        // Act
        var result = await CreateSubmitter().SubmitAsync([], "TenantA");

        // Assert
        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task SubmitAsync_WithMixedTenants_ShouldReturnError()
    {
        // Arrange
        var entries = new List<FingerprintChainEntry>
        {
            new() { TenantId = "TenantA" },
            new() { TenantId = "TenantB" }
        };

        // Act
        var result = await CreateSubmitter().SubmitAsync(entries, null);

        // Assert
        Assert.False(result.Accepted);
        Assert.Contains("mixed tenant partitions", result.Error!);
    }

    [Fact]
    public async Task SubmitAsync_WithMismatchedTenant_ShouldReturnError()
    {
        // Arrange
        var entries = new List<FingerprintChainEntry>
        {
            new() { TenantId = "TenantA" }
        };

        // Act
        var result = await CreateSubmitter().SubmitAsync(entries, "TenantB");

        // Assert
        Assert.False(result.Accepted);
        Assert.Contains("not match requested tenant partition", result.Error!);
    }

    // Mock HttpMessageHandler for HttpClient tests
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> SendAsyncFunc { get; set; } = null!;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return SendAsyncFunc(request, cancellationToken);
        }
    }

    [Fact]
    public async Task SubmitAsync_WithEntries_ShouldPostToServer()
    {
        // Arrange
        var entries = new List<FingerprintChainEntry>
        {
            new() { TenantId = "TenantA", ActionType = "ACTION", ActionName = "ActionA", PayloadHash = "HASH" }
        };

        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new ChainSubmissionResponse { Accepted = true })
                };
                return Task.FromResult(response);
            }
        };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://license.muonroi.com") };
        _httpClientFactory.CreateClient("LicenseServer").Returns(httpClient);
        _configs.Enterprise.TrustedLicenseServerHosts = ["license.muonroi.com"];

        // Act
        var result = await CreateSubmitter().SubmitAsync(entries, "TenantA");

        // Assert
        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task SubmitAsync_WithTrustedEndpointFailure_ShouldReturnError()
    {
        // Arrange
        _configs.Enterprise.EnableSecureDefaults = true;
        _configs.Enterprise.RequireTrustedEndpointInProduction = true;
        _configs.Online.EnableCertificatePinning = true;
        _configs.Online.ExpectedCertificateThumbprint = "thumbprint";
        _configs.EnforcementMode = LicenseEnforcementMode.Production;
        
        var entries = new List<FingerprintChainEntry> { new() { TenantId = "TenantA" } };
        var httpClient = new HttpClient { BaseAddress = new Uri("https://untrusted.com") };
        _httpClientFactory.CreateClient("LicenseServer").Returns(httpClient);

        // Act
        var result = await CreateSubmitter().SubmitAsync(entries, "TenantA");

        // Assert
        Assert.False(result.Accepted);
        Assert.Contains("requires a trusted license server endpoint", result.Error!);
    }

    [Fact]
    public async Task SubmitAsync_WithInvalidServerSignature_ShouldReturnError()
    {
        // Arrange
        _configs.Enterprise.EnableSecureDefaults = true;
        _configs.Enterprise.RequireServerResponseSignatureInProduction = true;
        _configs.Online.EnableCertificatePinning = true;
        _configs.Online.ExpectedCertificateThumbprint = "thumbprint";
        _configs.EnforcementMode = LicenseEnforcementMode.Production;
        _configs.Enterprise.TrustedLicenseServerHosts = ["license.muonroi.com"];

        var entries = new List<FingerprintChainEntry> { new() { TenantId = "TenantA" } };
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new ChainSubmissionResponse 
                    { 
                        Accepted = true, 
                        Signature = "INVALID_SIG" 
                    })
                };
                return Task.FromResult(response);
            }
        };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://license.muonroi.com") };
        _httpClientFactory.CreateClient("LicenseServer").Returns(httpClient);

        // Act
        var result = await CreateSubmitter().SubmitAsync(entries, "TenantA");

        // Assert
        Assert.False(result.Accepted);
        Assert.Contains("Invalid server signature", result.Error!);
    }

    // ─── durability: transient failures persist for retry ─────────────────────

    [Fact]
    public async Task SubmitAsync_TransientServerError_EnqueuesForRetry()
    {
        FakeFailedStore store = ArrangePostHandler(HttpStatusCode.ServiceUnavailable);
        var entries = new List<FingerprintChainEntry> { new() { TenantId = "TenantA" } };

        ChainSubmissionResponse result = await CreateSubmitter(failedStore: store).SubmitAsync(entries, "TenantA");

        Assert.False(result.Accepted);
        Assert.Single(store.Items);
        Assert.Equal(1, store.Items[0].AttemptCount);
    }

    [Fact]
    public async Task SubmitAsync_NetworkException_EnqueuesForRetry()
    {
        var store = new FakeFailedStore();
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (_, _) => throw new HttpRequestException("connection refused")
        };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://license.muonroi.com") };
        _httpClientFactory.CreateClient("LicenseServer").Returns(httpClient);
        _configs.Enterprise.TrustedLicenseServerHosts = ["license.muonroi.com"];

        var entries = new List<FingerprintChainEntry> { new() { TenantId = "TenantA" } };
        ChainSubmissionResponse result = await CreateSubmitter(failedStore: store).SubmitAsync(entries, "TenantA");

        Assert.False(result.Accepted);
        Assert.Single(store.Items);
    }

    [Fact]
    public async Task SubmitAsync_ClientError_DoesNotEnqueue()
    {
        FakeFailedStore store = ArrangePostHandler(HttpStatusCode.BadRequest);
        var entries = new List<FingerprintChainEntry> { new() { TenantId = "TenantA" } };

        ChainSubmissionResponse result = await CreateSubmitter(failedStore: store).SubmitAsync(entries, "TenantA");

        Assert.False(result.Accepted);
        Assert.Empty(store.Items); // 4xx is deterministic — must not poison the retry queue
    }

    [Fact]
    public async Task RetryPendingAsync_SuccessfulRetry_RemovesFromQueue()
    {
        var store = new FakeFailedStore();
        store.Items.Add(new PendingChainSubmission
        {
            Id = "p1",
            AttemptCount = 1,
            Request = new ChainSubmissionRequest
            {
                TenantId = "TenantA",
                Entries = [new FingerprintChainEntry { TenantId = "TenantA" }]
            }
        });
        ArrangePostHandler(HttpStatusCode.OK, accepted: true);

        ChainRetryResult result = await CreateSubmitter(failedStore: store).RetryPendingAsync();

        Assert.Equal(1, result.Succeeded);
        Assert.Empty(store.Items);
    }

    [Fact]
    public async Task RetryPendingAsync_DeadLettersAfterMaxAttempts()
    {
        var store = new FakeFailedStore();
        store.Items.Add(new PendingChainSubmission
        {
            Id = "p1",
            AttemptCount = 4, // one more failure reaches maxAttempts=5
            Request = new ChainSubmissionRequest
            {
                TenantId = "TenantA",
                Entries = [new FingerprintChainEntry { TenantId = "TenantA" }]
            }
        });
        ArrangePostHandler(HttpStatusCode.ServiceUnavailable);

        ChainRetryResult result = await CreateSubmitter(failedStore: store).RetryPendingAsync(maxAttempts: 5);

        Assert.Equal(1, result.Dropped);
        Assert.Empty(store.Items); // dead-lettered (removed) so the queue cannot grow unbounded
    }

    private FakeFailedStore ArrangePostHandler(HttpStatusCode status, bool accepted = false)
    {
        var store = new FakeFailedStore();
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (_, _) =>
            {
                HttpResponseMessage response = new(status)
                {
                    Content = JsonContent.Create(new ChainSubmissionResponse { Accepted = accepted })
                };
                return Task.FromResult(response);
            }
        };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://license.muonroi.com") };
        _httpClientFactory.CreateClient("LicenseServer").Returns(httpClient);
        _configs.Enterprise.TrustedLicenseServerHosts = ["license.muonroi.com"];
        return store;
    }

    private sealed class FakeFailedStore : IFailedChainSubmissionStore
    {
        public List<PendingChainSubmission> Items { get; } = [];

        public Task EnqueueAsync(PendingChainSubmission pending, CancellationToken cancellationToken = default)
        {
            Items.Add(pending);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PendingChainSubmission>> ListPendingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PendingChainSubmission>>([.. Items]);

        public Task UpdateAsync(PendingChainSubmission pending, CancellationToken cancellationToken = default)
            => Task.CompletedTask; // Items holds the same reference; attempt count mutated in place

        public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }
    }

    private ChainSubmitter CreateSubmitter(LicenseState? licenseState = null, IFailedChainSubmissionStore? failedStore = null)
    {
        licenseState ??= new LicenseState
        {
            IsValid = true,
            Tier = LicenseTier.Enterprise,
            Payload = new LicensePayload
            {
                LicenseId = "TEST_LICENSE",
                AllowedFeatures = [FreeTierFeatures.Premium.AuditTrail]
            }
        };

        return new ChainSubmitter(_configs, _httpClientFactory, _logger, licenseState, scopeFactory: null, failedStore: failedStore);
    }
}
