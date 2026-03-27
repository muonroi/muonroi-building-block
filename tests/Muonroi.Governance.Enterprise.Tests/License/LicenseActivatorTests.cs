using Muonroi.Governance.Abstractions.Integrity;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.License;
using Muonroi.Governance.Enterprise.License;
using Muonroi.Governance.Enterprise.ServerValidation;
using Muonroi.Governance.Enterprise.Tests.ServerValidation;
using Muonroi.Logging.Abstractions;
using Muonroi.Core.Abstractions.Interfaces;
using NSubstitute;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;

namespace Muonroi.Governance.Enterprise.Tests.License;

public class LicenseActivatorTests : IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LicenseConfigs _configs;
    private readonly IMJsonSerializeService _jsonSerializeService;
    private readonly IAssemblyHashCollector _assemblyHashCollector;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IMLog<LicenseActivator> _logger;
    private readonly string _tempDir;
    private readonly LicenseActivator _activator;

    public LicenseActivatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _configs = new LicenseConfigs
        {
            LicenseFilePath = Path.Combine(_tempDir, "license.txt"),
            ActivationProofPath = Path.Combine(_tempDir, "proof.json"),
            PublicKeyPath = Path.Combine(_tempDir, "public.pem")
        };
        _jsonSerializeService = Substitute.For<IMJsonSerializeService>();
        _assemblyHashCollector = Substitute.For<IAssemblyHashCollector>();
        _hostEnvironment = Substitute.For<IHostEnvironment>();
        _hostEnvironment.ContentRootPath.Returns(_tempDir);
        _logger = Substitute.For<IMLog<LicenseActivator>>();

        _activator = new LicenseActivator(
            _httpClientFactory,
            _configs,
            _jsonSerializeService,
            _assemblyHashCollector,
            _hostEnvironment,
            _logger);
    }

    [Fact]
    public async Task ActivateAsync_WithMissingLicenseFile_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<LicenseException>(() => _activator.ActivateAsync());
    }

    [Fact]
    public async Task ActivateAsync_WithValidLicense_ShouldSaveProof()
    {
        // Arrange
        File.WriteAllText(_configs.LicenseFilePath!, "TEST_KEY");
        
        var proof = new ActivationProof { OrganizationName = "TestOrg", Tier = LicenseTier.Enterprise };
        var activationResponse = new ActivationResponse { Success = true, Proof = proof };
        
        var handler = new ChainSubmitterTests.MockHttpMessageHandler
        {
            SendAsyncFunc = (req, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(activationResponse)
                };
                return Task.FromResult(response);
            }
        };
        var httpClient = new HttpClient(handler);
        _httpClientFactory.CreateClient("LicenseServer").Returns(httpClient);
        _jsonSerializeService.Serialize(Arg.Any<object>()).Returns("{}");

        // Act
        var result = await _activator.ActivateAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestOrg", result.OrganizationName);
        Assert.True(File.Exists(_configs.ActivationProofPath));
    }

    [Fact]
    public async Task TryActivateAsync_WithException_ShouldReturnFalse()
    {
        // Arrange
        // No license file - will cause ActivateAsync to throw LicenseException

        // Act
        var result = await _activator.TryActivateAsync();

        // Assert
        Assert.False(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}
