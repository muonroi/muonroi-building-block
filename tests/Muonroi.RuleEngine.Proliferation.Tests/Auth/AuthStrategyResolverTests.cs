using Moq;
using Muonroi.RuleEngine.Proliferation.Auth;
using Muonroi.RuleEngine.Proliferation.Models;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.RuleEngine.Proliferation.Tests.Auth;

public class AuthStrategyResolverTests
{
    private readonly Mock<IOAuth2TokenProvider> _oauth2ProviderMock = new();
    private readonly Mock<IMtlsHttpClientFactory> _mtlsFactoryMock = new();
    private readonly Mock<IApiKeyRotationService> _apiKeyRotationMock = new();

    private AuthStrategyResolver CreateResolver() =>
        new(_oauth2ProviderMock.Object, _mtlsFactoryMock.Object, _apiKeyRotationMock.Object);

    [Fact]
    public async Task ResolveAsync_None_ReturnsEmptyHeadersAndNullClient()
    {
        var resolver = CreateResolver();
        var config = new ExternalProjectConfig
        {
            ProjectId = "p1",
            TenantId = "t1",
            ExecutionEndpointUrl = "http://example.com",
            AuthStrategy = AuthStrategy.None
        };

        AuthResult result = await resolver.ResolveAsync(config, CancellationToken.None);

        Assert.Empty(result.Headers);
        Assert.Null(result.CustomHttpClient);
    }

    [Fact]
    public async Task ResolveAsync_StaticHeaders_CallsApiKeyRotationAndReturnsHeaders()
    {
        var resolver = CreateResolver();
        var headers = new Dictionary<string, string> { ["X-Api-Key"] = "mykey" };
        var rotatedHeaders = new Dictionary<string, string> { ["X-Api-Key"] = "rotatedkey" };
        var config = new ExternalProjectConfig
        {
            ProjectId = "p1",
            TenantId = "t1",
            ExecutionEndpointUrl = "http://example.com",
            AuthStrategy = AuthStrategy.StaticHeaders,
            ExecutionHeaders = headers
        };

        _apiKeyRotationMock
            .Setup(s => s.CheckAndRotateAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rotatedHeaders);

        AuthResult result = await resolver.ResolveAsync(config, CancellationToken.None);

        Assert.Equal("rotatedkey", result.Headers["X-Api-Key"]);
        Assert.Null(result.CustomHttpClient);
        _apiKeyRotationMock.Verify(s => s.CheckAndRotateAsync(config, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_OAuth2_CallsProviderAndReturnsBearerHeader()
    {
        var resolver = CreateResolver();
        var oauth2Config = new OAuth2Config
        {
            TokenEndpoint = "https://auth.example.com/token",
            ClientId = "client1",
            ClientSecret = "secret1"
        };
        var config = new ExternalProjectConfig
        {
            ProjectId = "p1",
            TenantId = "t1",
            ExecutionEndpointUrl = "http://example.com",
            AuthStrategy = AuthStrategy.OAuth2ClientCredentials,
            OAuth2 = oauth2Config
        };

        _oauth2ProviderMock
            .Setup(p => p.GetAccessTokenAsync(oauth2Config, It.IsAny<CancellationToken>()))
            .ReturnsAsync("my-access-token");

        AuthResult result = await resolver.ResolveAsync(config, CancellationToken.None);

        Assert.Equal("Bearer my-access-token", result.Headers["Authorization"]);
        Assert.Null(result.CustomHttpClient);
        _oauth2ProviderMock.Verify(p => p.GetAccessTokenAsync(oauth2Config, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_MutualTls_CallsFactoryAndReturnsCustomClient()
    {
        var resolver = CreateResolver();
        var mtlsConfig = new MtlsConfig { CertificatePath = "/certs/client.pfx" };
        var customClient = new HttpClient();
        var config = new ExternalProjectConfig
        {
            ProjectId = "p1",
            TenantId = "t1",
            ExecutionEndpointUrl = "http://example.com",
            AuthStrategy = AuthStrategy.MutualTls,
            Mtls = mtlsConfig
        };

        _mtlsFactoryMock
            .Setup(f => f.CreateClient("p1", mtlsConfig))
            .Returns(customClient);

        AuthResult result = await resolver.ResolveAsync(config, CancellationToken.None);

        Assert.Empty(result.Headers);
        Assert.Same(customClient, result.CustomHttpClient);
        _mtlsFactoryMock.Verify(f => f.CreateClient("p1", mtlsConfig), Times.Once);

        customClient.Dispose();
    }

    [Fact]
    public async Task ResolveAsync_OAuth2_NullOAuth2Config_ThrowsInvalidOperation()
    {
        var resolver = CreateResolver();
        var config = new ExternalProjectConfig
        {
            ProjectId = "p1",
            TenantId = "t1",
            ExecutionEndpointUrl = "http://example.com",
            AuthStrategy = AuthStrategy.OAuth2ClientCredentials,
            OAuth2 = null // missing config
        };

        await Assert.ThrowsAsync<MConfigurationException>(() => resolver.ResolveAsync(config, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_MutualTls_NullMtlsConfig_ThrowsInvalidOperation()
    {
        var resolver = CreateResolver();
        var config = new ExternalProjectConfig
        {
            ProjectId = "p1",
            TenantId = "t1",
            ExecutionEndpointUrl = "http://example.com",
            AuthStrategy = AuthStrategy.MutualTls,
            Mtls = null // missing config
        };

        await Assert.ThrowsAsync<MConfigurationException>(() => resolver.ResolveAsync(config, CancellationToken.None));
    }
}
