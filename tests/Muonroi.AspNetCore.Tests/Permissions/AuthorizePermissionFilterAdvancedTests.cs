using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.AspNetCore.Attributes;
using Muonroi.AspNetCore.Controllers.ActionFilters;
using Muonroi.AspNetCore.Exceptions;
using Muonroi.AspNetCore.Tests.Helpers;
using Muonroi.Caching.Memory.MultiLevel;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Enums;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.Logging.Abstractions;
using Muonroi.Tenancy.Core;
using NSubstitute;
using Xunit;
using Microsoft.AspNetCore.Authorization;

namespace Muonroi.AspNetCore.Tests.Permissions;

public class AuthorizePermissionFilterAdvancedTests
{
    private readonly TestDbContext _db;
    private readonly IMultiLevelCacheService _cache;
    private readonly IMLog<AuthorizePermissionFilter<TestDbContext>> _logger;
    private readonly AuthorizePermissionFilter<TestDbContext> _filter;

    public AuthorizePermissionFilterAdvancedTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new TestDbContext(options);
        _cache = Substitute.For<IMultiLevelCacheService>();
        _logger = Substitute.For<IMLog<AuthorizePermissionFilter<TestDbContext>>>();
        _filter = new AuthorizePermissionFilter<TestDbContext>(_db, _cache, _logger);
    }

    private (ActionExecutingContext, ActionExecutionDelegate) CreateContext(Endpoint endpoint, ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
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
    public async Task OnActionExecutionAsync_AllowAnonymous_Skips()
    {
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AllowAnonymousAttribute()), "test");
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var (context, next) = CreateContext(endpoint, user);

        await _filter.OnActionExecutionAsync(context, next);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoAttributes_Skips()
    {
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "test");
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var (context, next) = CreateContext(endpoint, user);

        await _filter.OnActionExecutionAsync(context, next);
    }

    [Fact]
    public async Task OnActionExecutionAsync_PdpAllowed_SkipsLocalRbac()
    {
        var userId = Guid.NewGuid();
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizePermissionAttribute("p1")), "test");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimConstants.UserIdentifier, userId.ToString()) }));
        var (context, next) = CreateContext(endpoint, user);

        var pdp = Substitute.For<IMPolicyDecisionService>();
        pdp.IsEnabled.Returns(true);
        pdp.EvaluateAsync(Arg.Any<MPolicyDecisionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MPolicyDecisionResult { IsAllowed = true, IsAuthoritative = true });

        context.HttpContext.RequestServices = Substitute.For<IServiceProvider>();
        context.HttpContext.RequestServices.GetService(typeof(IMPolicyDecisionService)).Returns(pdp);

        await _filter.OnActionExecutionAsync(context, next);

        await _cache.DidNotReceiveWithAnyArgs().GetOrSetAsync<List<string>>(default!, default!, default);
    }

    [Fact]
    public async Task OnActionExecutionAsync_PdpDenied_Throws()
    {
        var userId = Guid.NewGuid();
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizePermissionAttribute("p1")), "test");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimConstants.UserIdentifier, userId.ToString()) }));
        var (context, next) = CreateContext(endpoint, user);

        var pdp = Substitute.For<IMPolicyDecisionService>();
        pdp.IsEnabled.Returns(true);
        pdp.EvaluateAsync(Arg.Any<MPolicyDecisionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MPolicyDecisionResult { IsAllowed = false, IsAuthoritative = true });

        context.HttpContext.RequestServices = Substitute.For<IServiceProvider>();
        context.HttpContext.RequestServices.GetService(typeof(IMPolicyDecisionService)).Returns(pdp);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _filter.OnActionExecutionAsync(context, next));
    }

    [Fact]
    public async Task OnActionExecutionAsync_MatchModeAll_MissingOne_Throws()
    {
        var userId = Guid.NewGuid();
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(
            new AuthorizePermissionAttribute("p1", PermissionMatchMode.All),
            new AuthorizePermissionAttribute("p2", PermissionMatchMode.All)
        ), "test");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimConstants.UserIdentifier, userId.ToString()) }));
        var (context, next) = CreateContext(endpoint, user);

        _cache.GetOrSetAsync(Arg.Any<string>(), Arg.Any<Func<Task<List<string>?>>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "p1" });

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _filter.OnActionExecutionAsync(context, next));
    }

    [Fact]
    public async Task OnActionExecutionAsync_MatchModeAny_HasOne_Success()
    {
        var userId = Guid.NewGuid();
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(
            new AuthorizePermissionAttribute("p1", PermissionMatchMode.Any),
            new AuthorizePermissionAttribute("p2", PermissionMatchMode.Any)
        ), "test");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimConstants.UserIdentifier, userId.ToString()) }));
        var (context, next) = CreateContext(endpoint, user);

        _cache.GetOrSetAsync(Arg.Any<string>(), Arg.Any<Func<Task<List<string>?>>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "p2" });

        await _filter.OnActionExecutionAsync(context, next);
    }
}
