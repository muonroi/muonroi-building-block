namespace Muonroi.AspNetCore.Tests.Http;

/// <summary>
/// Unit tests for MCausalChainDelegatingHandler — verifies that upstream causal chain
/// headers are read from error responses and stored in Activity baggage.
/// </summary>
public class MCausalChainDelegatingHandlerTests
{
    // A minimal inner handler that returns a preconfigured response.
    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private static MCausalChain BuildChain(string service = "upstream-svc") => new()
    {
        OriginService = service,
        OriginErrorCode = "TEST-001",
        OriginLayer = MExceptionLayer.Application,
        Timestamp = DateTimeOffset.UtcNow
    };

    private static MCausalChainDelegatingHandler CreateHandler(MCausalChainOptions? options = null)
    {
        MCausalChainOptions opts = options ?? new MCausalChainOptions
        {
            ServiceName = "test-svc",
            MaxChainDepth = 10
        };
        return new MCausalChainDelegatingHandler(Options.Create(opts));
    }

    [Fact]
    public async Task Handler_ReadsChainFromErrorResponse_StoresBaggageKey()
    {
        // Arrange
        MCausalChain chain = BuildChain();
        string headerValue = MCausalChain.Serialize(chain);

        var innerResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        innerResponse.Headers.Add("muonroi-causal-chain", headerValue);

        MCausalChainDelegatingHandler handler = CreateHandler();
        handler.InnerHandler = new StubHandler(innerResponse);

        var invoker = new HttpMessageInvoker(handler);

        // Use a real Activity so baggage can be set
        using Activity activity = new Activity("test").Start();

        // Act
        _ = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test"), CancellationToken.None);

        // Assert
        string? baggage = activity.GetBaggageItem("muonroi.causal");
        Assert.NotNull(baggage);
        Assert.Equal(headerValue, baggage);
    }

    [Fact]
    public async Task Handler_IgnoresSuccessResponse_DoesNotSetBaggage()
    {
        // Arrange
        MCausalChain chain = BuildChain();
        string headerValue = MCausalChain.Serialize(chain);

        var innerResponse = new HttpResponseMessage(HttpStatusCode.OK);
        innerResponse.Headers.Add("muonroi-causal-chain", headerValue);

        MCausalChainDelegatingHandler handler = CreateHandler();
        handler.InnerHandler = new StubHandler(innerResponse);
        var invoker = new HttpMessageInvoker(handler);

        using Activity activity = new Activity("test").Start();

        // Act
        _ = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test"), CancellationToken.None);

        // Assert — 200 response should NOT set baggage
        string? baggage = activity.GetBaggageItem("muonroi.causal");
        Assert.Null(baggage);
    }

    [Fact]
    public async Task Handler_IgnoresResponseWithoutHeader_DoesNotSetBaggage()
    {
        // Arrange
        var innerResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        // No muonroi-causal-chain header

        MCausalChainDelegatingHandler handler = CreateHandler();
        handler.InnerHandler = new StubHandler(innerResponse);
        var invoker = new HttpMessageInvoker(handler);

        using Activity activity = new Activity("test").Start();

        // Act
        _ = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test"), CancellationToken.None);

        // Assert
        string? baggage = activity.GetBaggageItem("muonroi.causal");
        Assert.Null(baggage);
    }

    [Fact]
    public async Task Handler_IgnoresInvalidBase64Header_DoesNotSetBaggageAndNoException()
    {
        // Arrange — invalid base64 in header
        var innerResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        innerResponse.Headers.Add("muonroi-causal-chain", "!!!not-valid-base64!!!");

        MCausalChainDelegatingHandler handler = CreateHandler();
        handler.InnerHandler = new StubHandler(innerResponse);
        var invoker = new HttpMessageInvoker(handler);

        using Activity activity = new Activity("test").Start();

        // Act — should not throw
        HttpResponseMessage result = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://test"), CancellationToken.None);

        // Assert — baggage NOT set, no exception
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        string? baggage = activity.GetBaggageItem("muonroi.causal");
        Assert.Null(baggage);
    }

    [Fact]
    public async Task Handler_SkipsWhenActivityNull_NoExceptionThrown()
    {
        // Arrange — ensure no activity is running
        Activity.Current?.Stop();
        Activity.Current = null;

        MCausalChain chain = BuildChain();
        string headerValue = MCausalChain.Serialize(chain);

        var innerResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        innerResponse.Headers.Add("muonroi-causal-chain", headerValue);

        MCausalChainDelegatingHandler handler = CreateHandler();
        handler.InnerHandler = new StubHandler(innerResponse);
        var invoker = new HttpMessageInvoker(handler);

        // Act — Activity.Current is null, should NOT throw
        HttpResponseMessage result = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://test"), CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
    }

    [Fact]
    public void DI_RegistersOptionsAndHandler_CanResolveHandler()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();
        services.AddMuonroiCausalChain(opts =>
        {
            opts.ServiceName = "test-service";
        });

        IServiceProvider sp = services.BuildServiceProvider();

        // Act
        var resolvedOptions = sp.GetService<IOptions<MCausalChainOptions>>();
        var handler = sp.GetService<MCausalChainDelegatingHandler>();

        // Assert — options registered as singleton, handler as transient
        Assert.NotNull(resolvedOptions);
        Assert.Equal("test-service", resolvedOptions!.Value.ServiceName);
        Assert.NotNull(handler);
    }
}
