using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Muonroi.AspNetCore.Attributes;
using Muonroi.AspNetCore.Controllers.ActionFilters;
using Muonroi.AspNetCore.Exceptions;
using Muonroi.AspNetCore.Tests.Helpers;
using Muonroi.Caching.Memory.MultiLevel;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Enums;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.Data.EntityFrameworkCore.Entity.Identity;
using Muonroi.Tenancy.Core;
using Muonroi.Tenancy.Core.Legacy;
using TenantContext = Muonroi.Tenancy.Core.TenantContext;
using Xunit;

namespace Muonroi.AspNetCore.Tests.Permissions;

public class AuthorizePermissionFilterTests
{
    [Fact]
    public async Task User_With_Permission_Allows_Execution()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("auth_success").Options;
        using TestDbContext db = new(options);

        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MPermission perm = new()
        {
            Name = "perm",
            UiKey = "perm"
        };
        await db.Users.AddAsync(user);
        await db.Roles.AddAsync(role);
        await db.Permissions.AddAsync(perm);
        await db.SaveChangesAsync();
        MUserRole entity = new()
        {
            UserId = user.EntityId,
            RoleId = role.EntityId
        };
        await db.UserRoles.AddAsync(entity);
        MRolePermission permission = new()
        {
            RoleId = role.EntityId,
            PermissionId = perm.EntityId
        };
        await db.RolePermissions.AddAsync(permission);
        await db.SaveChangesAsync();

        TenantContext.CurrentTenantId = "t1";
        // Using a fake cache service for simplicity
        FakeCacheService svc = new(["perm"]);
        NullLogger<AuthorizePermissionFilter<TestDbContext>> logger = NullLogger<AuthorizePermissionFilter<TestDbContext>>.Instance;
        AuthorizePermissionFilter<TestDbContext> filter = new(db, svc, logger);

        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimConstants.UserIdentifier, user.EntityId.ToString())
            ]))
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizePermissionAttribute("perm")), "test");
        httpContext.SetEndpoint(endpoint);
        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        ActionExecutingContext executingContext =
            new(actionContext, [], new Dictionary<string, object?>(), new object());

        bool nextCalled = false;

        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
        }

        await filter.OnActionExecutionAsync(executingContext, Next);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task User_Without_Permission_Throws()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("auth_fail").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        TenantContext.CurrentTenantId = "t1";
        FakeCacheService svc = new([]);
        NullLogger<AuthorizePermissionFilter<TestDbContext>> logger = NullLogger<AuthorizePermissionFilter<TestDbContext>>.Instance;
        AuthorizePermissionFilter<TestDbContext> filter = new(db, svc, logger);

        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimConstants.UserIdentifier, user.EntityId.ToString())
            ]))
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizePermissionAttribute("perm")), "test");
        httpContext.SetEndpoint(endpoint);
        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        ActionExecutingContext executingContext =
            new(actionContext, [], new Dictionary<string, object?>(), new object());

        Task<ActionExecutedContext> Next()
        {
            return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
        }

        _ = await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            filter.OnActionExecutionAsync(executingContext, Next));
    }

    [Fact]
    public async Task Anonymous_User_Throws()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("auth_anonymous").Options;
        using TestDbContext db = new(options);
        TenantContext.CurrentTenantId = "t1";
        FakeCacheService svc = new([]);
        NullLogger<AuthorizePermissionFilter<TestDbContext>> logger = NullLogger<AuthorizePermissionFilter<TestDbContext>>.Instance;
        AuthorizePermissionFilter<TestDbContext> filter = new(db, svc, logger);

        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        Endpoint endpoint = new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizePermissionAttribute("perm")), "test");
        httpContext.SetEndpoint(endpoint);
        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        ActionExecutingContext executingContext =
            new(actionContext, [], new Dictionary<string, object?>(), new object());

        Task<ActionExecutedContext> Next()
        {
            return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
        }

        _ = await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            filter.OnActionExecutionAsync(executingContext, Next));
    }

    [Fact]
    public async Task AuthorizePermissionAttribute_UserWithoutPermission_IsBlocked()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using TestDbContext db = new(options);
        string userId = Guid.NewGuid().ToString();
        FakeCacheService cache = new(["OtherPermission"]);
        NullLogger<AuthorizePermissionFilter<TestDbContext>> logger = NullLogger<AuthorizePermissionFilter<TestDbContext>>.Instance;
        AuthorizePermissionFilter<TestDbContext> filter = new(db, cache, logger);

        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant";
        try
        {
            DefaultHttpContext httpContext = new()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimConstants.UserIdentifier, userId)]))
            };
            Endpoint endpoint = new(_ => Task.CompletedTask,
                new EndpointMetadataCollection(new AuthorizePermissionAttribute("SomePermission")),
                "test");
            httpContext.SetEndpoint(endpoint);
            ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
            ActionExecutingContext executingContext =
                new(actionContext, [], new Dictionary<string, object?>(), new object());

            bool nextCalled = false;

            Task<ActionExecutedContext> Next()
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
            }

            await Assert.ThrowsAsync<PermissionDeniedException>(() =>
                filter.OnActionExecutionAsync(executingContext, Next));
            Assert.False(nextCalled);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task AuthorizePermissionAttribute_UserWithPermission_Allows()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using TestDbContext db = new(options);
        string userId = Guid.NewGuid().ToString();
        FakeCacheService cache = new(["SomePermission"]);
        NullLogger<AuthorizePermissionFilter<TestDbContext>> logger = NullLogger<AuthorizePermissionFilter<TestDbContext>>.Instance;
        AuthorizePermissionFilter<TestDbContext> filter = new(db, cache, logger);

        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant";
        try
        {
            DefaultHttpContext httpContext = new()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimConstants.UserIdentifier, userId)]))
            };
            Endpoint endpoint = new(_ => Task.CompletedTask,
                new EndpointMetadataCollection(new AuthorizePermissionAttribute("SomePermission")),
                "test");
            httpContext.SetEndpoint(endpoint);
            ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
            ActionExecutingContext executingContext =
                new(actionContext, [], new Dictionary<string, object?>(), new object());

            bool nextCalled = false;

            Task<ActionExecutedContext> Next()
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
            }

            await filter.OnActionExecutionAsync(executingContext, Next);
            Assert.True(nextCalled);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task AuthorizePermissionAttribute_MixedAnyAll_WithMissingAll_IsBlocked()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using TestDbContext db = new(options);
        string userId = Guid.NewGuid().ToString();
        FakeCacheService cache = new(["orders.read"]);
        NullLogger<AuthorizePermissionFilter<TestDbContext>> logger = NullLogger<AuthorizePermissionFilter<TestDbContext>>.Instance;
        AuthorizePermissionFilter<TestDbContext> filter = new(db, cache, logger);

        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant";
        try
        {
            DefaultHttpContext httpContext = new()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimConstants.UserIdentifier, userId)]))
            };
            Endpoint endpoint = new(_ => Task.CompletedTask,
                new EndpointMetadataCollection(
                    new AuthorizePermissionAttribute("orders.read", PermissionMatchMode.Any),
                    new AuthorizePermissionAttribute("orders.manage", PermissionMatchMode.All)),
                "test");
            httpContext.SetEndpoint(endpoint);
            ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
            ActionExecutingContext executingContext =
                new(actionContext, [], new Dictionary<string, object?>(), new object());

            await Assert.ThrowsAsync<PermissionDeniedException>(() =>
                filter.OnActionExecutionAsync(executingContext,
                    () => Task.FromResult(new ActionExecutedContext(actionContext, [], new object()))));
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task AuthorizePermissionAttribute_MixedAnyAll_WithAllSatisfied_Allows()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using TestDbContext db = new(options);
        string userId = Guid.NewGuid().ToString();
        FakeCacheService cache = new(["orders.read", "orders.manage"]);
        NullLogger<AuthorizePermissionFilter<TestDbContext>> logger = NullLogger<AuthorizePermissionFilter<TestDbContext>>.Instance;
        AuthorizePermissionFilter<TestDbContext> filter = new(db, cache, logger);

        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant";
        try
        {
            DefaultHttpContext httpContext = new()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimConstants.UserIdentifier, userId)]))
            };
            Endpoint endpoint = new(_ => Task.CompletedTask,
                new EndpointMetadataCollection(
                    new AuthorizePermissionAttribute("orders.read", PermissionMatchMode.Any),
                    new AuthorizePermissionAttribute("orders.manage", PermissionMatchMode.All)),
                "test");
            httpContext.SetEndpoint(endpoint);
            ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
            ActionExecutingContext executingContext =
                new(actionContext, [], new Dictionary<string, object?>(), new object());

            bool nextCalled = false;
            await filter.OnActionExecutionAsync(executingContext, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
            });

            Assert.True(nextCalled);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task AuthorizePermissionAttribute_TenantMismatch_IsBlocked()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using TestDbContext db = new(options);
        string userId = Guid.NewGuid().ToString();
        FakeCacheService cache = new(["orders.read"]);
        NullLogger<AuthorizePermissionFilter<TestDbContext>> logger = NullLogger<AuthorizePermissionFilter<TestDbContext>>.Instance;
        AuthorizePermissionFilter<TestDbContext> filter = new(db, cache, logger);

        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-A";
        try
        {
            DefaultHttpContext httpContext = new()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimConstants.UserIdentifier, userId),
                    new Claim(ClaimConstants.TenantId, "tenant-B")
                ]))
            };
            Endpoint endpoint = new(_ => Task.CompletedTask,
                new EndpointMetadataCollection(new AuthorizePermissionAttribute("orders.read")),
                "test");
            httpContext.SetEndpoint(endpoint);
            ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
            ActionExecutingContext executingContext =
                new(actionContext, [], new Dictionary<string, object?>(), new object());

            await Assert.ThrowsAsync<PermissionDeniedException>(() =>
                filter.OnActionExecutionAsync(executingContext,
                    () => Task.FromResult(new ActionExecutedContext(actionContext, [], new object()))));
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task AuthorizePermissionAttribute_StrictMultiTenantMode_MissingTenantClaim_IsBlocked()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using TestDbContext db = new(options);
        string userId = Guid.NewGuid().ToString();
        FakeCacheService cache = new(["orders.read"]);
        NullLogger<AuthorizePermissionFilter<TestDbContext>> logger = NullLogger<AuthorizePermissionFilter<TestDbContext>>.Instance;
        AuthorizePermissionFilter<TestDbContext> filter = new(db, cache, logger);

        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-A";
        try
        {
            ServiceProvider sp = new ServiceCollection()
                .AddOptions()
                .Configure<MultiTenantConfigs>(x =>
                {
                    x.Enabled = true;
                    x.RequireTenantClaimForAuthenticatedUser = true;
                })
                .BuildServiceProvider();

            DefaultHttpContext httpContext = new()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimConstants.UserIdentifier, userId)],
                    "test")),
                RequestServices = sp
            };
            Endpoint endpoint = new(_ => Task.CompletedTask,
                new EndpointMetadataCollection(new AuthorizePermissionAttribute("orders.read")),
                "test");
            httpContext.SetEndpoint(endpoint);
            ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
            ActionExecutingContext executingContext =
                new(actionContext, [], new Dictionary<string, object?>(), new object());

            await Assert.ThrowsAsync<PermissionDeniedException>(() =>
                filter.OnActionExecutionAsync(executingContext,
                    () => Task.FromResult(new ActionExecutedContext(actionContext, [], new object()))));
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    private sealed class FakeCacheService(IEnumerable<string>? permissions) : IMultiLevelCacheService
    {
        private readonly List<string>? _permissions = permissions?.ToList();

        public Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, int? absoluteExpirationInMinutes = 1440,
            CancellationToken token = default)
        {
            if (typeof(T) == typeof(List<string>)) return Task.FromResult((T?)(object?)_permissions);

            return factory();
        }

        public Task SetAsync<T>(string key, T value, int? absoluteExpirationInMinutes = 1440,
            CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken token = default)
        {
            return Task.FromResult<T?>(default);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }
    }
}
