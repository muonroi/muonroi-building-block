namespace Muonroi.BuildingBlock.Test;

public class PolicyHandlerTests
{
    private class TestMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responder(request, CallCount));
        }
    }

    [Fact]
    public async Task SendAsync_Success_Returns_Response()
    {
        IAsyncPolicy<HttpResponseMessage> policy = Policy.NoOpAsync<HttpResponseMessage>();
        TestMessageHandler inner = new((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        PolicyHandler handler = new(policy)
        {
            InnerHandler = inner
        };
        HttpMessageInvoker invoker = new(handler);
        HttpResponseMessage resp = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://a"), CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task SendAsync_RetryPolicy_Retries_On_Failure()
    {
        IAsyncPolicy<HttpResponseMessage> policy = Policy<HttpResponseMessage>
            .HandleResult(r => r.StatusCode == HttpStatusCode.InternalServerError)
            .RetryAsync(2);
        TestMessageHandler inner = new((_, count) =>
            new HttpResponseMessage(count < 2 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK));
        PolicyHandler handler = new(policy)
        {
            InnerHandler = inner
        };
        HttpMessageInvoker invoker = new(handler);
        HttpResponseMessage resp = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://a"), CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task SendAsync_Inner_Throws_Propagates_Exception()
    {
        IAsyncPolicy<HttpResponseMessage> policy = Policy.NoOpAsync<HttpResponseMessage>();
        TestMessageHandler inner = new((_, _) => throw new InvalidOperationException());
        PolicyHandler handler = new(policy)
        {
            InnerHandler = inner
        };
        HttpMessageInvoker invoker = new(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://a"), CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_Null_Request_Throws()
    {
        IAsyncPolicy<HttpResponseMessage> policy = Policy.NoOpAsync<HttpResponseMessage>();
        TestMessageHandler inner = new((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        PolicyHandler handler = new(policy)
        {
            InnerHandler = inner
        };
        HttpMessageInvoker inv = new(handler);
        await Assert.ThrowsAsync<ArgumentNullException>(() => inv.SendAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Constructor_Allows_Null_Policy_But_Fails_On_Send()
    {
        PolicyHandler handler = new(null!)
        {
            InnerHandler = new TestMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK))
        };
        HttpMessageInvoker inv = new(handler);
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            inv.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://a"), CancellationToken.None));
    }
}
