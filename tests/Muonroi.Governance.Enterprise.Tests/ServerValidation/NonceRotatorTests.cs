namespace Muonroi.Governance.Enterprise.Tests.ServerValidation;

public class NonceRotatorTests : IDisposable
{
    private readonly string _root;
    private readonly LicenseConfigs _configs;
    private readonly LicenseStore _store;

    public NonceRotatorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "muonroi-nonce-rotator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        _configs = new LicenseConfigs
        {
            LicenseFilePath = Path.Combine(_root, "license.json")
        };

        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(_root);
        _store = new LicenseStore(environment, _configs, new MJsonSerializeService());
    }

    [Fact]
    public void UpdateLocalNonce_WhenPayloadExists_ShouldPersistNewNonce()
    {
        _store.Save(new LicensePayload { LicenseId = "LIC-001", ServerNonce = "old" });

        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("LicenseServer").Returns(new HttpClient(new StubHttpMessageHandler(
            new ChainSubmissionResponse { Accepted = true, NewNonce = "new-nonce" }))
        { BaseAddress = new Uri("https://license.muonroi.com") });
        ChainSubmitter submitter = new(
            _configs,
            httpClientFactory,
            null,
            new LicenseState
            {
                IsValid = true,
                Tier = LicenseTier.Enterprise,
                Payload = new LicensePayload { LicenseId = "LIC-001", AllowedFeatures = [FreeTierFeatures.Premium.AuditTrail] }
            },
            null);

        NonceRotator rotator = new(_configs, submitter, _store, Substitute.For<IMLog<NonceRotator>>());
        MethodInfo updateMethod = typeof(NonceRotator).GetMethod("UpdateLocalNonce", BindingFlags.NonPublic | BindingFlags.Instance)!;

        updateMethod.Invoke(rotator, ["new-nonce"]);

        _store.Load()!.ServerNonce.Should().Be("new-nonce");
    }

    [Fact]
    public async Task RotateAsync_WhenSubmissionRejected_ShouldNotOverwriteNonce()
    {
        _store.Save(new LicensePayload { LicenseId = "LIC-001", ServerNonce = "old" });

        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("LicenseServer").Returns(new HttpClient(new StubHttpMessageHandler(
            new ChainSubmissionResponse { Accepted = false, Error = "denied" }))
        { BaseAddress = new Uri("https://license.muonroi.com") });
        ChainSubmitter submitter = new(
            _configs,
            httpClientFactory,
            null,
            new LicenseState
            {
                IsValid = true,
                Tier = LicenseTier.Enterprise,
                Payload = new LicensePayload { LicenseId = "LIC-001", AllowedFeatures = [FreeTierFeatures.Premium.AuditTrail] }
            },
            null);

        NonceRotator rotator = new(_configs, submitter, _store, Substitute.For<IMLog<NonceRotator>>());

        await rotator.RotateAsync([new FingerprintChainEntry { Sequence = 1, TenantId = "tenant-a" }], "tenant-a");

        _store.Load()!.ServerNonce.Should().Be("old");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private sealed class StubHttpMessageHandler(ChainSubmissionResponse response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(response)
            });
        }
    }
}
