using System.Net;
using System.Text;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Muonroi.RuleEngine.Proliferation.Auth;
using Muonroi.RuleEngine.Proliferation.Models;
using Microsoft.Extensions.Http;

namespace Muonroi.RuleEngine.Proliferation.Tests.Auth;

public class CachingOAuth2TokenProviderTests
{
    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock
            .Setup(f => f.CreateClient("OAuth2Auth"))
            .Returns(new HttpClient(handler) { BaseAddress = null });
        return factoryMock.Object;
    }

    private static Mock<HttpMessageHandler> CreateHandlerMock(string token, int expiresIn)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        string responseJson = JsonSerializer.Serialize(new
        {
            access_token = token,
            expires_in = expiresIn,
            token_type = "Bearer"
        });

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

        return handlerMock;
    }

    private static OAuth2Config MakeConfig(string clientId = "client1") => new()
    {
        TokenEndpoint = "https://auth.example.com/token",
        ClientId = clientId,
        ClientSecret = "secret1",
        Scope = "api"
    };

    [Fact]
    public async Task GetAccessTokenAsync_CacheMiss_FetchesTokenFromEndpoint()
    {
        var handlerMock = CreateHandlerMock("fresh-token", 3600);
        var factory = CreateFactory(handlerMock.Object);
        var provider = new CachingOAuth2TokenProvider(factory);
        var config = MakeConfig();

        string token = await provider.GetAccessTokenAsync(config, CancellationToken.None);

        Assert.Equal("fresh-token", token);
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetAccessTokenAsync_CacheHit_ReturnsCachedTokenWithoutHttpCall()
    {
        var handlerMock = CreateHandlerMock("cached-token", 3600);
        var factory = CreateFactory(handlerMock.Object);
        var provider = new CachingOAuth2TokenProvider(factory);
        var config = MakeConfig();

        // First call — populates cache
        string token1 = await provider.GetAccessTokenAsync(config, CancellationToken.None);
        // Second call — should hit cache
        string token2 = await provider.GetAccessTokenAsync(config, CancellationToken.None);

        Assert.Equal("cached-token", token1);
        Assert.Equal("cached-token", token2);

        // HTTP should only be called once
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateToken_RemovesCachedEntry_NextCallFetchesNew()
    {
        int callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                string json = JsonSerializer.Serialize(new
                {
                    access_token = $"token-{callCount}",
                    expires_in = 3600
                });
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            });

        var factory = CreateFactory(handlerMock.Object);
        var provider = new CachingOAuth2TokenProvider(factory);
        var config = MakeConfig();

        string first = await provider.GetAccessTokenAsync(config, CancellationToken.None);
        provider.InvalidateToken(config);
        string second = await provider.GetAccessTokenAsync(config, CancellationToken.None);

        Assert.Equal("token-1", first);
        Assert.Equal("token-2", second);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_DifferentConfigs_MaintainSeparateCaches()
    {
        int callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                string json = JsonSerializer.Serialize(new
                {
                    access_token = $"token-{callCount}",
                    expires_in = 3600
                });
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            });

        var factory = CreateFactory(handlerMock.Object);
        var provider = new CachingOAuth2TokenProvider(factory);
        var config1 = MakeConfig("client-a");
        var config2 = MakeConfig("client-b");

        string t1 = await provider.GetAccessTokenAsync(config1, CancellationToken.None);
        string t2 = await provider.GetAccessTokenAsync(config2, CancellationToken.None);
        string t1Again = await provider.GetAccessTokenAsync(config1, CancellationToken.None);

        Assert.Equal("token-1", t1);
        Assert.Equal("token-2", t2);
        Assert.Equal("token-1", t1Again); // cache hit, no new call
        Assert.Equal(2, callCount);
    }
}
