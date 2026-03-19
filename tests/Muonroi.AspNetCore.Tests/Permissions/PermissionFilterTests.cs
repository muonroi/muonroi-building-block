using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Muonroi.AspNetCore.Attributes;
using Muonroi.AspNetCore.Controllers.ActionFilters;
using Muonroi.AspNetCore.Exceptions;
using Muonroi.AspNetCore.Tests.Helpers;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Enums;
using Muonroi.Tenancy.Core;
using Muonroi.Tenancy.Core.Legacy;
using TenantContext = Muonroi.Tenancy.Core.TenantContext;
using Xunit;

namespace Muonroi.AspNetCore.Tests.Permissions;

public class PermissionFilterTests
{
    [Fact]
    public async Task AllowAnonymous_AllowsExecution()
    {
        PermissionFilter<TestPerm> filter = new(Substitute.For<IMLog<PermissionFilter<TestPerm>>>());
        DefaultHttpContext ctx = new();
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()), "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());
        bool called = false;

        Task<ActionExecutedContext> Next()
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(ac, [], new object()));
        }

        await filter.OnActionExecutionAsync(exc, Next);
        Assert.True(called);
    }

    [Fact]
    public async Task NoPermissionAttributes_AllowsExecution()
    {
        PermissionFilter<TestPerm> filter = new(Substitute.For<IMLog<PermissionFilter<TestPerm>>>());
        DefaultHttpContext ctx = new();
        Endpoint endpoint = new(_ => Task.CompletedTask, new EndpointMetadataCollection(), "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());
        bool called = false;

        Task<ActionExecutedContext> Next()
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(ac, [], new object()));
        }

        await filter.OnActionExecutionAsync(exc, Next);
        Assert.True(called);
    }

    [Fact]
    public async Task MissingPermissionClaim_Throws()
    {
        PermissionFilter<TestPerm> filter = new(Substitute.For<IMLog<PermissionFilter<TestPerm>>>());
        DefaultHttpContext ctx = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new PermissionAttribute<TestPerm>(TestPerm.Read)), "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());

        Task<ActionExecutedContext> Next()
        {
            return Task.FromResult(new ActionExecutedContext(ac, [], new object()));
        }

        await Assert.ThrowsAsync<PermissionDeniedException>(() => filter.OnActionExecutionAsync(exc, Next));
    }

    [Fact]
    public async Task HasRequiredPermission_Allows()
    {
        PermissionFilter<TestPerm> filter = new(Substitute.For<IMLog<PermissionFilter<TestPerm>>>());
        ClaimsIdentity id = new([new Claim(ClaimConstants.Permission, ((long)TestPerm.Read).ToString())]);
        DefaultHttpContext ctx = new()
        {
            User = new ClaimsPrincipal(id)
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new PermissionAttribute<TestPerm>(TestPerm.Read)), "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());
        bool called = false;

        Task<ActionExecutedContext> Next()
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(ac, [], new object()));
        }

        await filter.OnActionExecutionAsync(exc, Next);
        Assert.True(called);
    }

    [Fact]
    public async Task LackingPermission_Throws()
    {
        PermissionFilter<TestPerm> filter = new(Substitute.For<IMLog<PermissionFilter<TestPerm>>>());
        ClaimsIdentity id = new([new Claim(ClaimConstants.Permission, ((long)TestPerm.Read).ToString())]);
        DefaultHttpContext ctx = new()
        {
            User = new ClaimsPrincipal(id)
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new PermissionAttribute<TestPerm>(TestPerm.Write)), "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());

        Task<ActionExecutedContext> Next()
        {
            return Task.FromResult(new ActionExecutedContext(ac, [], new object()));
        }

        await Assert.ThrowsAsync<PermissionDeniedException>(() => filter.OnActionExecutionAsync(exc, Next));
    }

    [Fact]
    public async Task UserWithPermission_ExecutesNextAndLeavesResultNull()
    {
        PermissionFilter<TestPerm> filter = new(Substitute.For<IMLog<PermissionFilter<TestPerm>>>());
        ClaimsIdentity id = new([new Claim(ClaimConstants.Permission, ((long)TestPerm.Read).ToString())]);
        DefaultHttpContext ctx = new()
        {
            User = new ClaimsPrincipal(id)
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new PermissionAttribute<TestPerm>(TestPerm.One),
                new PermissionAttribute<TestPerm>(TestPerm.Read)), "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());
        bool called = false;

        Task<ActionExecutedContext> Next()
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(ac, [], new object()));
        }

        await filter.OnActionExecutionAsync(exc, Next);
        Assert.True(called);
        Assert.Null(exc.Result);
    }

    [Fact]
    public async Task UserWithoutPermission_DoesNotCallNextAndThrows()
    {
        PermissionFilter<TestPerm> filter = new(Substitute.For<IMLog<PermissionFilter<TestPerm>>>());
        ClaimsIdentity id = new([new Claim(ClaimConstants.Permission, ((long)TestPerm.One).ToString())]);
        DefaultHttpContext ctx = new()
        {
            User = new ClaimsPrincipal(id)
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new PermissionAttribute<TestPerm>(TestPerm.Read)), "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());
        bool called = false;

        Task<ActionExecutedContext> Next()
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(ac, [], new object()));
        }

        await Assert.ThrowsAsync<PermissionDeniedException>(() => filter.OnActionExecutionAsync(exc, Next));
        Assert.False(called);
    }

    [Fact]
    public async Task AllMode_Requires_All_Configured_Permissions()
    {
        PermissionFilter<TestPerm> filter = new(Substitute.For<IMLog<PermissionFilter<TestPerm>>>());
        ClaimsIdentity id = new([new Claim(ClaimConstants.Permission, ((long)TestPerm.Write).ToString())]);
        DefaultHttpContext ctx = new()
        {
            User = new ClaimsPrincipal(id)
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new PermissionAttribute<TestPerm>(TestPerm.One, PermissionMatchMode.All),
                new PermissionAttribute<TestPerm>(TestPerm.Read, PermissionMatchMode.All)),
            "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());
        bool called = false;

        Task<ActionExecutedContext> Next()
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(ac, [], new object()));
        }

        await filter.OnActionExecutionAsync(exc, Next);
        Assert.True(called);
    }

    [Fact]
    public async Task MixedAnyAllMode_MissingAllRequirement_ShouldThrow()
    {
        PermissionFilter<TestPerm> filter = new(Substitute.For<IMLog<PermissionFilter<TestPerm>>>());
        ClaimsIdentity id = new([new Claim(ClaimConstants.Permission, ((long)TestPerm.Read).ToString())]);
        DefaultHttpContext ctx = new()
        {
            User = new ClaimsPrincipal(id)
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new PermissionAttribute<TestPerm>(TestPerm.Read, PermissionMatchMode.Any),
                new PermissionAttribute<TestPerm>(TestPerm.One, PermissionMatchMode.All)),
            "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());

        Task<ActionExecutedContext> Next()
        {
            return Task.FromResult(new ActionExecutedContext(ac, [], new object()));
        }

        await Assert.ThrowsAsync<PermissionDeniedException>(() => filter.OnActionExecutionAsync(exc, Next));
    }

    [Fact]
    public async Task TenantMismatch_Throws()
    {
        PermissionFilter<TestPerm> filter = new(Substitute.For<IMLog<PermissionFilter<TestPerm>>>());
        ClaimsIdentity id = new(
        [
            new Claim(ClaimConstants.Permission, ((long)TestPerm.Read).ToString()),
            new Claim(ClaimConstants.TenantId, "tenant-B")
        ]);
        DefaultHttpContext ctx = new()
        {
            User = new ClaimsPrincipal(id)
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new PermissionAttribute<TestPerm>(TestPerm.Read)), "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());

        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-A";
        try
        {
            await Assert.ThrowsAsync<PermissionDeniedException>(() =>
                filter.OnActionExecutionAsync(exc,
                    () => Task.FromResult(new ActionExecutedContext(ac, [], new object()))));
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task TenantMatch_Allows()
    {
        PermissionFilter<TestPerm> filter = new(Substitute.For<IMLog<PermissionFilter<TestPerm>>>());
        ClaimsIdentity id = new(
        [
            new Claim(ClaimConstants.Permission, ((long)TestPerm.Read).ToString()),
            new Claim(ClaimConstants.TenantId, "tenant-A")
        ]);
        DefaultHttpContext ctx = new()
        {
            User = new ClaimsPrincipal(id)
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new PermissionAttribute<TestPerm>(TestPerm.Read)), "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());
        bool called = false;

        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-A";
        try
        {
            await filter.OnActionExecutionAsync(exc, () =>
            {
                called = true;
                return Task.FromResult(new ActionExecutedContext(ac, [], new object()));
            });

            Assert.True(called);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task StrictMultiTenantMode_MissingTenantClaim_Throws()
    {
        PermissionFilter<TestPerm> filter = new(Substitute.For<IMLog<PermissionFilter<TestPerm>>>());
        ClaimsIdentity id = new(
        [
            new Claim(ClaimConstants.Permission, ((long)TestPerm.Read).ToString())
        ], "test");
        DefaultHttpContext ctx = new()
        {
            User = new ClaimsPrincipal(id)
        };
        ServiceProvider serviceProvider = new ServiceCollection()
            .AddOptions()
            .Configure<MultiTenantConfigs>(x =>
            {
                x.Enabled = true;
                x.RequireTenantClaimForAuthenticatedUser = true;
            })
            .BuildServiceProvider();
        ctx.RequestServices = serviceProvider;
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new PermissionAttribute<TestPerm>(TestPerm.Read)), "test");
        ctx.SetEndpoint(endpoint);
        ActionContext ac = new(ctx, new RouteData(), new ActionDescriptor());
        ActionExecutingContext exc = new(ac, [], new Dictionary<string, object?>(), new object());

        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-A";
        try
        {
            await Assert.ThrowsAsync<PermissionDeniedException>(() =>
                filter.OnActionExecutionAsync(exc,
                    () => Task.FromResult(new ActionExecutedContext(ac, [], new object()))));
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }
}

