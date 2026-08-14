namespace Muonroi.Governance.Enterprise.Tests.License;

public class LicenseHeartbeatServiceTests : IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LicenseConfigs _configs;
    private readonly LicenseState _state;
    private readonly LicenseRuntimeStatus _runtimeStatus;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IMLog<LicenseHeartbeatService> _logger;
    private readonly string _tempDir;
    private readonly LicenseHeartbeatService _service;

    public LicenseHeartbeatServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _configs = new LicenseConfigs
        {
            Mode = LicenseMode.Online,
            Online = new OnlineLicenseConfigs
            {
                Endpoint = "https://license.muonroi.com",
                EnableHeartbeat = true,
                HeartbeatIntervalMinutes = 1
            }
        };
        _state = new LicenseState
        {
            ActivationProof = new ActivationProof
            {
                LicenseId = "L1",
                ProofId = "P1",
                HeartbeatNonce = "N1"
            }
        };
        _runtimeStatus = new LicenseRuntimeStatus();
        _hostEnvironment = Substitute.For<IHostEnvironment>();
        _hostEnvironment.ContentRootPath.Returns(_tempDir);
        _logger = Substitute.For<IMLog<LicenseHeartbeatService>>();

        _service = new LicenseHeartbeatService(
            _httpClientFactory,
            _configs,
            _state,
            _runtimeStatus,
            _hostEnvironment,
            _logger);
    }

    private Task CallSendHeartbeatAsync(CancellationToken cancellationToken)
    {
        var method = typeof(LicenseHeartbeatService).GetMethod("SendHeartbeatAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        return (Task)method!.Invoke(_service, [cancellationToken])!;
    }

    [Fact]
    public async Task SendHeartbeatAsync_WithSuccessfulHeartbeat_ShouldUpdateStatus()
    {
        // Arrange
        var heartbeatResponse = new LicenseHeartbeatResponse
        {
            Success = true,
            NewNonce = "N2",
            CheckedAtUtc = DateTimeOffset.UtcNow
        };
        
        var handler = new ChainSubmitterTests.MockHttpMessageHandler
        {
            SendAsyncFunc = (req, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(heartbeatResponse)
                };
                return Task.FromResult(response);
            }
        };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://license.muonroi.com") };
        _httpClientFactory.CreateClient("LicenseServer").Returns(httpClient);

        // Act
        await CallSendHeartbeatAsync(CancellationToken.None);

        // Assert
        Assert.Equal("N2", _runtimeStatus.CurrentHeartbeatNonce);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WithRevocation_ShouldStartGracePeriod()
    {
        // Arrange
        var graceUntil = DateTimeOffset.UtcNow.AddHours(1);
        var heartbeatResponse = new LicenseHeartbeatResponse
        {
            Success = true,
            IsRevoked = true,
            GraceUntilUtc = graceUntil
        };

        var handler = new ChainSubmitterTests.MockHttpMessageHandler
        {
            SendAsyncFunc = (req, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(heartbeatResponse)
                };
                return Task.FromResult(response);
            }
        };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://license.muonroi.com") };
        _httpClientFactory.CreateClient("LicenseServer").Returns(httpClient);

        // Act
        await CallSendHeartbeatAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(_runtimeStatus.RevocationGraceUntilUtc);
        Assert.Equal(graceUntil, _runtimeStatus.RevocationGraceUntilUtc);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}
