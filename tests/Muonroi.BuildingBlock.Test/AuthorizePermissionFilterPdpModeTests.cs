using Muonroi.Governance;

namespace Muonroi.BuildingBlock.Test;

public class AuthorizePermissionFilterPdpModeTests
{
    [Fact]
    public async Task PdpAuthoritativeAllow_BypassesLocalPermissionLookup()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u1",
            EmailAddress = "u1@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IMPolicyDecisionService>(new StubPdpService(MPolicyDecisionResult.Allowed("pdp.opa")))
            .BuildServiceProvider();

        AuthorizePermissionFilter<TestDbContext> filter = new(
            db,
            new PassThroughCacheService(),
            NullLogger<AuthorizePermissionFilter<TestDbContext>>.Instance);

        DefaultHttpContext httpContext = new()
        {
            RequestServices = serviceProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimConstants.UserIdentifier, user.EntityId.ToString())]))
        };
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizePermissionAttribute("orders.read")),
            "test"));

        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        ActionExecutingContext executingContext = new(actionContext, [], new Dictionary<string, object?>(), new object());

        bool called = false;
        await filter.OnActionExecutionAsync(executingContext, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
        });

        Assert.True(called);
    }

    [Fact]
    public async Task PdpAuthoritativeDeny_BlocksEvenWhenLocalWouldAllow()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u2",
            EmailAddress = "u2@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        MRole role = new()
        {
            Name = "r2",
            DisplayName = "r2",
            NormalizedName = "R2"
        };
        MPermission perm = new()
        {
            Name = "orders.read",
            UiKey = "orders.read"
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

        ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IMPolicyDecisionService>(new StubPdpService(MPolicyDecisionResult.Denied("pdp.opa")))
            .BuildServiceProvider();

        AuthorizePermissionFilter<TestDbContext> filter = new(
            db,
            new PassThroughCacheService(),
            NullLogger<AuthorizePermissionFilter<TestDbContext>>.Instance);

        DefaultHttpContext httpContext = new()
        {
            RequestServices = serviceProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimConstants.UserIdentifier, user.EntityId.ToString())]))
        };
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizePermissionAttribute("orders.read")),
            "test"));

        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        ActionExecutingContext executingContext = new(actionContext, [], new Dictionary<string, object?>(), new object());

        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            filter.OnActionExecutionAsync(executingContext,
                () => Task.FromResult(new ActionExecutedContext(actionContext, [], new object()))));
    }

    [Fact]
    public async Task PdpFallback_UsesLocalPermissionLookup()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u3",
            EmailAddress = "u3@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        MRole role = new()
        {
            Name = "r3",
            DisplayName = "r3",
            NormalizedName = "R3"
        };
        MPermission perm = new()
        {
            Name = "orders.read",
            UiKey = "orders.read"
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

        ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IMPolicyDecisionService>(
                new StubPdpService(MPolicyDecisionResult.LocalFallback("local.fallback")))
            .BuildServiceProvider();

        AuthorizePermissionFilter<TestDbContext> filter = new(
            db,
            new PassThroughCacheService(),
            NullLogger<AuthorizePermissionFilter<TestDbContext>>.Instance);

        DefaultHttpContext httpContext = new()
        {
            RequestServices = serviceProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimConstants.UserIdentifier, user.EntityId.ToString())]))
        };
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizePermissionAttribute("orders.read")),
            "test"));

        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        ActionExecutingContext executingContext = new(actionContext, [], new Dictionary<string, object?>(), new object());

        bool called = false;
        await filter.OnActionExecutionAsync(executingContext, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
        });

        Assert.True(called);
    }

    private sealed class StubPdpService(MPolicyDecisionResult result) : IMPolicyDecisionService
    {
        public bool IsEnabled => true;

        public Task<MPolicyDecisionResult> EvaluateAsync(MPolicyDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class PassThroughCacheService : IMultiLevelCacheService
    {
        public Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, int? absoluteExpirationInMinutes = 1440,
            CancellationToken token = default)
        {
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
