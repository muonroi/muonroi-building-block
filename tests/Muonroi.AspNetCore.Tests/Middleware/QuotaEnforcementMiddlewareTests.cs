namespace Muonroi.AspNetCore.Tests.Middleware;

public class QuotaEnforcementMiddlewareTests
{
    private readonly ITenantQuotaTracker _quotaTracker = Substitute.For<ITenantQuotaTracker>();
    private readonly IMLog<QuotaEnforcementMiddleware> _logger = Substitute.For<IMLog<QuotaEnforcementMiddleware>>();
    private readonly ISystemExecutionContextAccessor _contextAccessor = Substitute.For<ISystemExecutionContextAccessor>();

    private QuotaEnforcementMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new QuotaEnforcementMiddleware(next, _quotaTracker, _logger, _contextAccessor);
    }

    private static SystemExecutionContext CreateContext(string? tenantId)
    {
        return new SystemExecutionContext(
            tenantId: tenantId,
            userId: null,
            username: null,
            correlationId: Guid.NewGuid().ToString(),
            accessToken: null,
            apiKey: null,
            isAuthenticated: false,
            permissions: [],
            sourceType: "http");
    }

    [Fact]
    public async Task InvokeAsync_NoTenantId_CallsNext()
    {
        bool nextCalled = false;
        _contextAccessor.Get().Returns(CreateContext(null));

        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext);

        nextCalled.Should().BeTrue();
        await _quotaTracker.DidNotReceive().CheckQuotaAsync(
            Arg.Any<string>(), Arg.Any<QuotaType>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_EmptyTenantId_CallsNext()
    {
        bool nextCalled = false;
        _contextAccessor.Get().Returns(CreateContext("  "));

        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_QuotaAllowed_IncrementsAndCallsNext()
    {
        bool nextCalled = false;
        _contextAccessor.Get().Returns(CreateContext("tenant-1"));
        _quotaTracker.CheckQuotaAsync("tenant-1", QuotaType.ApiRequestsPerMinute, 1, Arg.Any<CancellationToken>())
            .Returns(true);

        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext);

        nextCalled.Should().BeTrue();
        await _quotaTracker.Received(1).IncrementUsageAsync(
            "tenant-1", QuotaType.ApiRequestsPerMinute, 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_QuotaExceeded_Returns429()
    {
        bool nextCalled = false;
        _contextAccessor.Get().Returns(CreateContext("tenant-1"));
        _quotaTracker.CheckQuotaAsync("tenant-1", QuotaType.ApiRequestsPerMinute, 1, Arg.Any<CancellationToken>())
            .Returns(false);

        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        nextCalled.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        httpContext.Response.Headers.RetryAfter.ToString().Should().Be("60");

        httpContext.Response.Body.Position = 0;
        string body = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        body.Should().Contain("rate_limit_exceeded");
    }

    [Fact]
    public async Task InvokeAsync_QuotaExceeded_DoesNotIncrementUsage()
    {
        _contextAccessor.Get().Returns(CreateContext("tenant-1"));
        _quotaTracker.CheckQuotaAsync("tenant-1", QuotaType.ApiRequestsPerMinute, 1, Arg.Any<CancellationToken>())
            .Returns(false);

        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        await _quotaTracker.DidNotReceive().IncrementUsageAsync(
            Arg.Any<string>(), Arg.Any<QuotaType>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
