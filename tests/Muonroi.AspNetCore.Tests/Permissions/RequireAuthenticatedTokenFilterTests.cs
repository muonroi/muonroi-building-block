using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Muonroi.AspNetCore.Controllers.ActionFilters;
using Muonroi.AspNetCore.Tests.Helpers;
using Muonroi.Caching.Memory.MultiLevel;
using Muonroi.Logging.Abstractions;
using NSubstitute;
using Xunit;
using Microsoft.AspNetCore.Authorization;

namespace Muonroi.AspNetCore.Tests.Permissions;

public class RequireAuthenticatedTokenFilterTests
{
    private readonly TestDbContext _db;
    private readonly IMultiLevelCacheService _cache;
    private readonly IMLog<MDbContext> _logger;
    private readonly RequireAuthenticatedTokenFilter<TestDbContext, TestPerm> _filter;

    public RequireAuthenticatedTokenFilterTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new TestDbContext(options);
        _cache = Substitute.For<IMultiLevelCacheService>();
        _logger = Substitute.For<IMLog<MDbContext>>();
        _filter = new RequireAuthenticatedTokenFilter<TestDbContext, TestPerm>(_db, _cache, _logger);
    }

    private (ActionExecutingContext, ActionExecutionDelegate) CreateContext(Endpoint endpoint)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(endpoint);
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var executingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        
        ActionExecutionDelegate next = () =>
        {
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        };

        return (executingContext, next);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoEndpoint_Skips()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        ActionExecutionDelegate next = () => Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));

        await _filter.OnActionExecutionAsync(context, next);
    }

    [Fact]
    public async Task OnActionExecutionAsync_AllowAnonymous_Skips()
    {
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AllowAnonymousAttribute()), "test");
        var (context, next) = CreateContext(endpoint);

        await _filter.OnActionExecutionAsync(context, next);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoAuthorize_Skips()
    {
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "test");
        var (context, next) = CreateContext(endpoint);

        await _filter.OnActionExecutionAsync(context, next);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoToken_ReturnsUnauthorized()
    {
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()), "test");
        var (context, next) = CreateContext(endpoint);

        await _filter.OnActionExecutionAsync(context, next);

        Assert.IsType<UnauthorizedResult>(context.Result);
    }
}
