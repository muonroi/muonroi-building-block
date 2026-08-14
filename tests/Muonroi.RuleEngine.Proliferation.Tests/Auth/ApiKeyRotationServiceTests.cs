namespace Muonroi.RuleEngine.Proliferation.Tests.Auth;

public class ApiKeyRotationServiceTests
{
    private static ExternalProjectConfig MakeConfig(
        IReadOnlyDictionary<string, string>? headers = null,
        DateTimeOffset? expiresAt = null,
        string? rotationUrl = null)
    {
        return new ExternalProjectConfig
        {
            ProjectId = "p1",
            TenantId = "t1",
            ExecutionEndpointUrl = "http://example.com",
            ExecutionHeaders = headers ?? new Dictionary<string, string> { ["X-Api-Key"] = "oldkey" },
            ApiKeyRotation = expiresAt.HasValue || rotationUrl != null
                ? new ApiKeyRotationConfig { ApiKeyExpiresAt = expiresAt, ApiKeyRotationUrl = rotationUrl }
                : null
        };
    }

    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));
        return factoryMock.Object;
    }

    [Fact]
    public async Task CheckAndRotateAsync_NullApiKeyRotation_ReturnsExistingHeaders()
    {
        var service = new ApiKeyRotationService(Mock.Of<IHttpClientFactory>(), null);
        var config = MakeConfig(expiresAt: null, rotationUrl: null);

        IReadOnlyDictionary<string, string> result = await service.CheckAndRotateAsync(config, CancellationToken.None);

        Assert.Same(config.ExecutionHeaders, result);
    }

    [Fact]
    public async Task CheckAndRotateAsync_ExpiryMoreThanOneHourAway_ReturnsExistingHeaders()
    {
        var service = new ApiKeyRotationService(Mock.Of<IHttpClientFactory>(), null);
        var config = MakeConfig(
            expiresAt: DateTimeOffset.UtcNow.AddHours(2),
            rotationUrl: "https://auth.example.com/rotate");

        IReadOnlyDictionary<string, string> result = await service.CheckAndRotateAsync(config, CancellationToken.None);

        Assert.Same(config.ExecutionHeaders, result);
    }

    [Fact]
    public async Task CheckAndRotateAsync_ExpiryWithinOneHour_CallsRotationUrl()
    {
        string rotatedKey = "newkey-xyz";
        DateTimeOffset newExpiry = DateTimeOffset.UtcNow.AddHours(24);

        string responseJson = JsonSerializer.Serialize(new
        {
            apiKey = rotatedKey,
            expiresAt = newExpiry.ToString("o")
        });

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var service = new ApiKeyRotationService(CreateFactory(handlerMock.Object), null);
        var config = MakeConfig(
            headers: new Dictionary<string, string> { ["X-Api-Key"] = "oldkey" },
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30), // within 1h
            rotationUrl: "https://auth.example.com/rotate");

        IReadOnlyDictionary<string, string> result = await service.CheckAndRotateAsync(config, CancellationToken.None);

        Assert.Equal(rotatedKey, result["X-Api-Key"]);
    }

    [Fact]
    public async Task CheckAndRotateAsync_RotationFails_ReturnsExistingHeadersGracefully()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("server error")
            });

        var service = new ApiKeyRotationService(CreateFactory(handlerMock.Object), null);
        var config = MakeConfig(
            headers: new Dictionary<string, string> { ["X-Api-Key"] = "oldkey" },
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30),
            rotationUrl: "https://auth.example.com/rotate");

        // Should NOT throw — graceful fallback
        IReadOnlyDictionary<string, string> result = await service.CheckAndRotateAsync(config, CancellationToken.None);

        Assert.Equal("oldkey", result["X-Api-Key"]);
    }

    [Fact]
    public async Task CheckAndRotateAsync_NullExpiry_ReturnsExistingHeaders()
    {
        var service = new ApiKeyRotationService(Mock.Of<IHttpClientFactory>(), null);
        var config = MakeConfig(
            expiresAt: null, // null expiry = never expires
            rotationUrl: "https://auth.example.com/rotate");
        // Need to manually set ApiKeyRotation with null expiry but a rotation URL
        var configWithRotation = config with
        {
            ApiKeyRotation = new ApiKeyRotationConfig
            {
                ApiKeyExpiresAt = null,
                ApiKeyRotationUrl = "https://auth.example.com/rotate"
            }
        };

        IReadOnlyDictionary<string, string> result = await service.CheckAndRotateAsync(configWithRotation, CancellationToken.None);

        Assert.Same(configWithRotation.ExecutionHeaders, result);
    }
}
