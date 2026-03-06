namespace Muonroi.BuildingBlock.Test;

public class AuthenticateHeaderHandlerTests
{
    private class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest;
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }

    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("fail");
    }

    [Fact]
    public async Task SendAsync_Adds_Headers_When_Present()
    {
        RecordingHandler inner = new();
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ApiKey"] = "key" }).Build();
        MAuthenticateInfoContext ctx = new(true)
        {
            AccessToken = "Bearer token",
            CorrelationId = "c"
        };
        AuthenticateHeaderHandler handler = new(NullLogger<AuthenticateHeaderHandler>.Instance, ctx, config)
        {
            InnerHandler = inner
        };
        HttpRequestMessage req = new(HttpMethod.Get, "http://localhost");

        HttpMessageInvoker invoker = new(handler);
        await invoker.SendAsync(req, CancellationToken.None);

        Assert.Equal("Bearer", inner.LastRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("token", inner.LastRequest!.Headers.Authorization?.Parameter);
        Assert.True(inner.LastRequest.Headers.Contains(CustomHeader.ApiKey));
        Assert.Equal("c", inner.LastRequest.Headers.GetValues(CustomHeader.CorrelationId).First());
    }

    [Fact]
    public async Task SendAsync_NoHeaders_When_Missing()
    {
        RecordingHandler inner = new();
        IConfiguration config = new ConfigurationBuilder().Build();
        MAuthenticateInfoContext ctx = new(false);
        AuthenticateHeaderHandler handler = new(NullLogger<AuthenticateHeaderHandler>.Instance, ctx, config)
        {
            InnerHandler = inner
        };
        HttpRequestMessage req = new(HttpMethod.Get, "http://localhost");

        HttpMessageInvoker invoker = new(handler);
        await invoker.SendAsync(req, CancellationToken.None);

        Assert.Null(inner.LastRequest!.Headers.Authorization);
        Assert.False(inner.LastRequest.Headers.Contains(CustomHeader.ApiKey));
    }

    [Fact]
    public async Task SendAsync_Propagates_Exception_From_Inner_Handler()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        MAuthenticateInfoContext ctx = new(false);
        AuthenticateHeaderHandler handler = new(NullLogger<AuthenticateHeaderHandler>.Instance, ctx, config)
        {
            InnerHandler = new ThrowingHandler()
        };
        HttpRequestMessage req = new(HttpMethod.Get, "http://localhost");

        HttpMessageInvoker invoker = new(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.SendAsync(req, CancellationToken.None));
    }

}
