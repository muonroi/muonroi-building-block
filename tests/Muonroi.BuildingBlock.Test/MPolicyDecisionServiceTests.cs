using Muonroi.Governance;
using System.Net;

namespace Muonroi.BuildingBlock.Test;

public class MPolicyDecisionServiceTests
{
    [Fact]
    public async Task EvaluateAsync_Disabled_ReturnsLocalFallback()
    {
        MPolicyDecisionConfigs configs = new()
        {
            Enabled = false
        };
        MPolicyDecisionService service = new(
            configs,
            new TestHttpClientFactory(new TestHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK))));

        MPolicyDecisionRequest request = new()
        {
            DecisionType = "permission-key"
        };
        MPolicyDecisionResult result = await service.EvaluateAsync(request);

        Assert.False(result.IsAuthoritative);
        Assert.True(result.UsedFallback);
        Assert.Equal("local.disabled", result.DecisionSource);
    }

    [Fact]
    public async Task EvaluateAsync_OpaBoolResponse_ReturnsAuthoritativeAllow()
    {
        MPolicyDecisionConfigs configs = new()
        {
            Enabled = true,
            Endpoint = "http://localhost:8181",
            Provider = MPolicyDecisionProvider.Opa
        };
        MPolicyDecisionService service = new(
            configs,
            new TestHttpClientFactory(new TestHandler((_, _) =>
            {
                HttpResponseMessage message = new(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"result\":true}")
                };
                return message;
            })));

        MPolicyDecisionRequest request = new()
        {
            DecisionType = "permission-key"
        };
        MPolicyDecisionResult result = await service.EvaluateAsync(request);

        Assert.True(result.IsAuthoritative);
        Assert.True(result.IsAllowed);
        Assert.Equal("pdp.opa", result.DecisionSource);
    }

    [Fact]
    public async Task EvaluateAsync_OpaNestedAllow_ReturnsAuthoritativeDeny()
    {
        MPolicyDecisionConfigs configs = new()
        {
            Enabled = true,
            Endpoint = "http://localhost:8181",
            Provider = MPolicyDecisionProvider.Opa
        };
        MPolicyDecisionService service = new(
            configs,
            new TestHttpClientFactory(new TestHandler((_, _) =>
            {
                HttpResponseMessage message = new(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"result\":{\"allow\":false}}")
                };
                return message;
            })));

        MPolicyDecisionRequest request = new()
        {
            DecisionType = "permission-key"
        };
        MPolicyDecisionResult result = await service.EvaluateAsync(request);

        Assert.True(result.IsAuthoritative);
        Assert.False(result.IsAllowed);
        Assert.Equal("pdp.opa", result.DecisionSource);
    }

    [Fact]
    public async Task EvaluateAsync_OpenFgaAllowedResponse_ReturnsAuthoritativeAllow()
    {
        MPolicyDecisionConfigs configs = new()
        {
            Enabled = true,
            Endpoint = "http://localhost:8080",
            Provider = MPolicyDecisionProvider.OpenFga
        };
        MPolicyDecisionService service = new(
            configs,
            new TestHttpClientFactory(new TestHandler((_, _) =>
            {
                HttpResponseMessage message = new(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"allowed\":true}")
                };
                return message;
            })));

        MPolicyDecisionRequest request = new()
        {
            DecisionType = "permission-key"
        };
        MPolicyDecisionResult result = await service.EvaluateAsync(request);

        Assert.True(result.IsAuthoritative);
        Assert.True(result.IsAllowed);
        Assert.Equal("pdp.openfga", result.DecisionSource);
    }

    [Fact]
    public async Task EvaluateAsync_RemoteError_WithFallbackMode_ReturnsLocalFallback()
    {
        MPolicyDecisionConfigs configs = new()
        {
            Enabled = true,
            Endpoint = "http://localhost:8181",
            Provider = MPolicyDecisionProvider.Opa,
            FailureMode = MPolicyDecisionFailureMode.FallbackToLocal
        };
        MPolicyDecisionService service = new(
            configs,
            new TestHttpClientFactory(new TestHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError))));

        MPolicyDecisionRequest request = new()
        {
            DecisionType = "permission-key"
        };
        MPolicyDecisionResult result = await service.EvaluateAsync(request);

        Assert.False(result.IsAuthoritative);
        Assert.True(result.UsedFallback);
        Assert.Equal("local.fallback", result.DecisionSource);
    }

    [Fact]
    public async Task EvaluateAsync_RemoteError_WithDenyMode_ReturnsAuthoritativeDeny()
    {
        MPolicyDecisionConfigs configs = new()
        {
            Enabled = true,
            Endpoint = "http://localhost:8181",
            Provider = MPolicyDecisionProvider.Opa,
            FailureMode = MPolicyDecisionFailureMode.Deny
        };
        MPolicyDecisionService service = new(
            configs,
            new TestHttpClientFactory(new TestHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError))));

        MPolicyDecisionRequest request = new()
        {
            DecisionType = "permission-key"
        };
        MPolicyDecisionResult result = await service.EvaluateAsync(request);

        Assert.True(result.IsAuthoritative);
        Assert.False(result.IsAllowed);
        Assert.Equal("pdp.failure", result.DecisionSource);
    }

    private sealed class TestHttpClientFactory(TestHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            HttpClient client = new(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };
            return client;
        }
    }

    private sealed class TestHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request, cancellationToken));
        }
    }
}
