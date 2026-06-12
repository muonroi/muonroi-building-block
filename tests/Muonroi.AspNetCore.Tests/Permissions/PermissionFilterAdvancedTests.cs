using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.AspNetCore.Attributes;
using Muonroi.AspNetCore.Controllers.ActionFilters;
using Muonroi.AspNetCore.Exceptions;
using Muonroi.AspNetCore.Tests.Helpers;
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

public class PermissionFilterAdvancedTests
{
    private readonly IMLog<PermissionFilter<TestPerm>> _logger;
    private readonly PermissionFilter<TestPerm> _filter;

    public PermissionFilterAdvancedTests()
    {
        _logger = Substitute.For<IMLog<PermissionFilter<TestPerm>>>();
        _filter = new PermissionFilter<TestPerm>(_logger);
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
    public async Task OnActionExecutionAsync_PdpAllowed_SkipsLocalCheck()
    {
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new PermissionAttribute<TestPerm>(TestPerm.Read)), "test");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimConstants.UserIdentifier, Guid.NewGuid().ToString()) }));
        var (context, next) = CreateContext(endpoint, user);

        var pdp = Substitute.For<IMPolicyDecisionService>();
        pdp.IsEnabled.Returns(true);
        pdp.EvaluateAsync(Arg.Any<MPolicyDecisionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MPolicyDecisionResult { IsAllowed = true, IsAuthoritative = true });

        context.HttpContext.RequestServices = Substitute.For<IServiceProvider>();
        context.HttpContext.RequestServices.GetService(typeof(IMPolicyDecisionService)).Returns(pdp);

        await _filter.OnActionExecutionAsync(context, next);
    }

    [Fact]
    public async Task OnActionExecutionAsync_MissingPermissions_Throws()
    {
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new PermissionAttribute<TestPerm>(TestPerm.Read)), "test");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimConstants.UserIdentifier, Guid.NewGuid().ToString()) }));
        var (context, next) = CreateContext(endpoint, user);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _filter.OnActionExecutionAsync(context, next));
    }

    [Fact]
    public async Task OnActionExecutionAsync_HasPermissions_Success()
    {
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new PermissionAttribute<TestPerm>(TestPerm.Read)), "test");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] 
        { 
            new Claim(ClaimConstants.UserIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimConstants.Permission, ((long)TestPerm.Read).ToString())
        }));
        var (context, next) = CreateContext(endpoint, user);

        await _filter.OnActionExecutionAsync(context, next);
    }
}
