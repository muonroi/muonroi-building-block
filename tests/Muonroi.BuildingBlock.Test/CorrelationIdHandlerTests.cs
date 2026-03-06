namespace Muonroi.BuildingBlock.Test;

public class CorrelationIdHandlerTests
{
    private class StubHandler : DelegatingHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response);
        }
    }

    private class ThrowingHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("fail");
        }
    }

    [Fact]
    public async Task SendAsync_Adds_Correlation_And_ApiKey_When_Present()
    {
        MAuthenticateInfoContext ctx = new(true)
        {
            CorrelationId = "cid",
            ApiKey = "api"
        };
        StubHandler inner = new();
        CorrelationIdHandler handler = new(ctx)
        {
            InnerHandler = inner
        };
        using HttpMessageInvoker invoker = new(handler);
        HttpRequestMessage req = new(HttpMethod.Get, "http://localhost/");

        HttpResponseMessage resp = await invoker.SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.True(inner.Request!.Headers.TryGetValues(CustomHeader.CorrelationId, out IEnumerable<string>? cid));
        Assert.Equal("cid", cid.Single());
        Assert.True(inner.Request.Headers.TryGetValues(CustomHeader.ApiKey, out IEnumerable<string>? api));
        Assert.Equal("api", api.Single());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SendAsync_Does_Not_Add_Correlation_When_Missing(string? correlation)
    {
        MAuthenticateInfoContext ctx = new(false)
        {
            CorrelationId = correlation ?? string.Empty,
            ApiKey = "key"
        };
        StubHandler inner = new();
        CorrelationIdHandler handler = new(ctx)
        {
            InnerHandler = inner
        };
        using HttpMessageInvoker invoker = new(handler);
        HttpRequestMessage req = new(HttpMethod.Get, "http://localhost/");

        _ = await invoker.SendAsync(req, CancellationToken.None);

        Assert.False(inner.Request!.Headers.Contains(CustomHeader.CorrelationId));
        Assert.True(inner.Request.Headers.Contains(CustomHeader.ApiKey));
    }

    [Fact]
    public async Task SendAsync_Propagates_Exception_From_Inner_Handler()
    {
        MAuthenticateInfoContext ctx = new(false)
        {
            CorrelationId = "cid"
        };
        CorrelationIdHandler handler = new(ctx)
        {
            InnerHandler = new ThrowingHandler()
        };
        using HttpMessageInvoker invoker = new(handler);
        HttpRequestMessage req = new(HttpMethod.Get, "http://localhost/");

        await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.SendAsync(req, CancellationToken.None));
    }
}
