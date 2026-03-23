using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.Abstractions.Integrity;
using Muonroi.Governance.Enterprise.License;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Governance.License;
using Muonroi.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Muonroi.Governance.Enterprise.Tests.License;

public class LicenseActivationHostedServiceTests : IDisposable
{
    private readonly string _root;

    public LicenseActivationHostedServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "muonroi-license-activation-hosted-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task StartAsync_WhenOfflineMode_ShouldSkipActivation()
    {
        LicenseConfigs configs = new() { Mode = LicenseMode.Offline };
        LicenseActivator activator = CreateActivator(configs);

        LicenseActivationHostedService service = new(
            activator,
            configs,
            new LicenseState(),
            null);

        await service.StartAsync(CancellationToken.None);

        File.Exists(Path.Combine(_root, "activation-proof.json")).Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyLicensed_ShouldSkipActivation()
    {
        LicenseConfigs configs = new()
        {
            Mode = LicenseMode.Online,
            ActivationProofPath = Path.Combine(_root, "activation-proof.json"),
            Online = new OnlineLicenseConfigs { Endpoint = "https://license.muonroi.com" }
        };
        LicenseActivator activator = CreateActivator(configs);

        LicenseActivationHostedService service = new(
            activator,
            configs,
            new LicenseState { IsValid = true, Tier = LicenseTier.Enterprise },
            null);

        await service.StartAsync(CancellationToken.None);

        File.Exists(configs.ActivationProofPath!).Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_WhenOnlineAndNotLicensed_ShouldPersistActivationProof()
    {
        LicenseConfigs configs = new()
        {
            Mode = LicenseMode.Online,
            LicenseFilePath = Path.Combine(_root, "license.txt"),
            ActivationProofPath = Path.Combine(_root, "activation-proof.json"),
            PublicKeyPath = Path.Combine(_root, "public.pem"),
            Online = new OnlineLicenseConfigs { Endpoint = "https://license.muonroi.com" }
        };
        await File.WriteAllTextAsync(configs.LicenseFilePath!, "TEST_KEY");

        ActivationProof proof = new()
        {
            LicenseId = "LIC-001",
            LicenseKey = "TEST_KEY",
            OrganizationName = "Muonroi",
            Tier = LicenseTier.Enterprise,
            ActivatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            ActivatedEnvironment = "Test",
            Signature = "sig"
        };

        LicenseActivator activator = CreateActivator(
            configs,
            new StubHttpMessageHandler((request, _) =>
            {
                if (request.RequestUri!.AbsoluteUri.Contains("/api/v1/activate", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new ActivationResponse { Success = true, Proof = proof })
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("PUBLIC-KEY")
                };
            }));

        LicenseActivationHostedService service = new(
            activator,
            configs,
            new LicenseState { IsValid = false, Tier = LicenseTier.Free },
            null);

        await service.StartAsync(CancellationToken.None);

        File.Exists(configs.ActivationProofPath!).Should().BeTrue();
    }

    private LicenseActivator CreateActivator(LicenseConfigs configs, HttpMessageHandler? handler = null)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new(handler ?? new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError)))
        {
            BaseAddress = new Uri("https://license.muonroi.com")
        };
        httpClientFactory.CreateClient("LicenseServer").Returns(httpClient);

        IHostEnvironment hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.ContentRootPath.Returns(_root);

        IAssemblyHashCollector collector = Substitute.For<IAssemblyHashCollector>();
        collector.Collect().Returns([]);

        return new LicenseActivator(
            httpClientFactory,
            configs,
            new MJsonSerializeService(),
            collector,
            hostEnvironment,
            Substitute.For<IMLog<LicenseActivator>>());
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(send(request, cancellationToken));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
