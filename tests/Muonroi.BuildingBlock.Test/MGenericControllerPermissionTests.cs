using Muonroi.Governance.License;

namespace Muonroi.BuildingBlock.Test;

public class MGenericControllerPermissionTests
{
    private sealed class StubMediator : IMediator
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Task.FromResult(default(TResponse)!);
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => Task.CompletedTask;
        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(null);
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) => AsyncEnumerableHelper.Empty<TResponse>();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => AsyncEnumerableHelper.Empty<object?>();
    }

    private sealed class AllowLicenseGuard : ILicenseGuard
    {
        private static readonly LicenseState State = LicenseState.CreateFree();
        public LicenseState Current => State;
        public LicenseTier Tier => State.Tier;
        public bool IsFreeMode => false;

        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
            string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName) => true;
        public void EnsureFeature(string featureName) { }
        public void RecordAction(LicenseActionContext context) { }
        public string GetChainToken() => "test";
        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
            => decryptor("k", encryptedData);
    }

    private sealed class GenericPermissionDbContext(DbContextOptions<GenericPermissionDbContext> options)
        : MDbContext(options, new StubMediator())
    {
        public DbSet<ProductEntity> Products { get; set; } = null!;
    }

    private sealed class ProductEntity : MEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    [GenericCrudPermission(PermissionPrefix = "Products")]
    private sealed class ProductController(
        GenericPermissionDbContext db,
        ILicenseGuard guard,
        MTokenInfo tokenInfo,
        IConfiguration configuration,
        IMDateTimeService dateTimeService) : MGenericController<ProductEntity, GenericPermissionDbContext>(
            db,
            guard,
            tokenInfo,
            configuration,
            dateTimeService)
    {
    }

    [Fact]
    public async Task Get_WithoutCacheService_UsesDbPermissionCheck_AndAllowsWhenGranted()
    {
        DbContextOptions<GenericPermissionDbContext> options = new DbContextOptionsBuilder<GenericPermissionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using GenericPermissionDbContext db = new(options);
        UserContext.CurrentUserGuid = null;

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
            Name = "admin",
            DisplayName = "Admin",
            NormalizedName = "ADMIN"
        };
        MPermission permission = new()
        {
            Name = "Products.View",
            UiKey = "Products.View"
        };

        await db.Users.AddAsync(user);
        await db.Roles.AddAsync(role);
        await db.Permissions.AddAsync(permission);
        ProductEntity entity = new()
        {
            Name = "p1"
        };
        await db.Products.AddAsync(entity);
        await db.SaveChangesAsync();
        MUserRole userRole = new()
        {
            UserId = user.EntityId,
            RoleId = role.EntityId
        };
        await db.UserRoles.AddAsync(userRole);
        MRolePermission rolePermission = new()
        {
            RoleId = role.EntityId,
            PermissionId = permission.EntityId
        };
        await db.RolePermissions.AddAsync(rolePermission);
        await db.SaveChangesAsync();

        ProductController controller = new(db, new AllowLicenseGuard(), new MTokenInfo { MultiTenantEnabled = false },
            new ConfigurationBuilder().AddInMemoryCollection().Build(), new MDateTimeService());

        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, user.EntityId.ToString())
            ])),
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        IActionResult response = await controller.Get(cancellationToken: CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
    }

    [Fact]
    public async Task Get_WithoutCacheService_DeniesWhenPermissionMissing()
    {
        DbContextOptions<GenericPermissionDbContext> options = new DbContextOptionsBuilder<GenericPermissionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using GenericPermissionDbContext db = new(options);
        UserContext.CurrentUserGuid = null;

        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        ProductEntity entity = new()
        {
            Name = "p1"
        };
        await db.Products.AddAsync(entity);
        await db.SaveChangesAsync();

        ProductController controller = new(db, new AllowLicenseGuard(), new MTokenInfo { MultiTenantEnabled = false },
            new ConfigurationBuilder().AddInMemoryCollection().Build(), new MDateTimeService());

        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, user.EntityId.ToString())
            ])),
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        IActionResult response = await controller.Get(cancellationToken: CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
    }
}
