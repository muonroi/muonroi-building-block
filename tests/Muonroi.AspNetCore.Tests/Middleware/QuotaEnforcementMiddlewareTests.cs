namespace Muonroi.AspNetCore.Tests.Middleware;

public class QuotaEnforcementMiddlewareTests
{
    private readonly RequestDelegate _next;
    private readonly ITenantQuotaTracker _quotaTracker;
    private readonly IMLog<QuotaEnforcementMiddleware> _logger;
    private readonly ISystemExecutionContextAccessor _contextAccessor;
    private readonly QuotaEnforcementMiddleware _middleware;

    public QuotaEnforcementMiddlewareTests()
    {
        _next = Substitute.For<RequestDelegate>();
        _quotaTracker = Substitute.For<ITenantQuotaTracker>();
        _logger = Substitute.For<IMLog<QuotaEnforcementMiddleware>>();
        _contextAccessor = Substitute.For<ISystemExecutionContextAccessor>();
        _middleware = new QuotaEnforcementMiddleware(_next, _quotaTracker, _logger, _contextAccessor);
    }

    private static SystemExecutionContext CreateContext(string? tenantId)
    {
        return new SystemExecutionContext(
            tenantId,
            null,
            null,
            Guid.NewGuid().ToString(),
            null,
            null,
            tenantId != null,
            [],
            "test"
        );
    }

    [Fact]
    public async Task InvokeAsync_NoTenantId_CallsNext()
    {
        var context = new DefaultHttpContext();
        _contextAccessor.Get().Returns(CreateContext(null));

        await _middleware.InvokeAsync(context);

        await _next.Received(1)(context);
    }

    [Fact]
    public async Task InvokeAsync_QuotaAllowed_IncrementsAndCallsNext()
    {
        var context = new DefaultHttpContext();
        _contextAccessor.Get().Returns(CreateContext("tenant1"));
        _quotaTracker.CheckQuotaAsync("tenant1", QuotaType.ApiRequestsPerMinute, 1, Arg.Any<CancellationToken>()).Returns(true);

        await _middleware.InvokeAsync(context);

        await _quotaTracker.Received(1).IncrementUsageAsync("tenant1", QuotaType.ApiRequestsPerMinute, 1, Arg.Any<CancellationToken>());
        await _next.Received(1)(context);
    }

    [Fact]
    public async Task InvokeAsync_QuotaExceeded_Returns429()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream(); 
        
        _contextAccessor.Get().Returns(CreateContext("tenant1"));
        _quotaTracker.CheckQuotaAsync("tenant1", QuotaType.ApiRequestsPerMinute, 1, Arg.Any<CancellationToken>()).Returns(false);

        await _middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        await _next.DidNotReceive()(context);
    }
}
