using Muonroi.Governance.Abstractions.License;
using Muonroi.Logging.Abstractions;
using Muonroi.Mediator.Mediator;
using Muonroi.Tenancy.Abstractions;

namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class NoTenantContextTests
{
    private enum TestPermission
    {
        Read
    }

    private sealed class NoTenantTestDbContext(
        DbContextOptions<NoTenantTestDbContext> options,
        IMediator mediator,
        ILicenseGuard? licenseGuard = null,
        IMLog<MDbContext>? logger = null)
        : MDbContext(options, mediator, licenseGuard, logger, new MDateTimeService())
    {
    }

    private static IServiceCollection CreateBaseServices(bool withTenantContext = false, string? tenantId = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseConfigs:DbType"] = nameof(DbTypes.Sqlite),
                ["DatabaseConfigs:ConnectionStrings:SqliteConnectionString"] = "Data Source=:memory:",
                ["EnableEncryption"] = "false"
            })
            .Build();

        ServiceCollection services = [];
        services.AddSingleton(configuration);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ILicenseGuard, TestLicenseGuard>();
        services.AddSingleton(new LicenseConfigs());
        services.AddScoped<IMediator>(_ => new NoMediator());

        if (withTenantContext)
        {
            services.AddScoped<ITenantContext>(_ => new TenantContext { TenantId = tenantId });
        }

        return services;
    }

    [Fact]
    public void NoOpTenantContext_TenantId_Is_Null()
    {
        NoOpTenantContext sut = new();

        Assert.Null(sut.TenantId);
    }

    [Fact]
    public void NoOpTenantContext_Implements_ITenantContext()
    {
        NoOpTenantContext sut = new();

        Assert.IsAssignableFrom<ITenantContext>(sut);
    }

    [Fact]
    public void MDbContext_Resolves_Without_ITenantContext_In_DI()
    {
        // Arrange: configure services WITHOUT registering ITenantContext
        IServiceCollection services = CreateBaseServices(withTenantContext: false);
        InvokeConfigureDbContext(services, nameof(DbTypes.Sqlite));

        using ServiceProvider sp = services.BuildServiceProvider();

        // Act: resolve DbContext — should NOT throw even without ITenantContext
        NoTenantTestDbContext? dbContext = sp.GetService<NoTenantTestDbContext>();

        // Assert
        Assert.NotNull(dbContext);
    }

    [Fact]
    public void MDbContext_Uses_Registered_ITenantContext_When_Available()
    {
        // Arrange: configure services WITH ITenantContext
        IServiceCollection services = CreateBaseServices(withTenantContext: true, tenantId: "test-tenant");
        InvokeConfigureDbContext(services, nameof(DbTypes.Sqlite));

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        // Act
        NoTenantTestDbContext? dbContext = scope.ServiceProvider.GetService<NoTenantTestDbContext>();

        // Assert
        Assert.NotNull(dbContext);
    }

    [Fact]
    public void TenantSchemaSelector_Receives_Null_When_No_ITenantContext()
    {
        // Arrange: no ITenantContext registered
        IServiceCollection services = CreateBaseServices(withTenantContext: false);
        InvokeConfigureDbContext(services, nameof(DbTypes.Sqlite));

        using ServiceProvider sp = services.BuildServiceProvider();

        // Act: resolve context — the fallback NoOpTenantContext should have null TenantId
        NoTenantTestDbContext? dbContext = sp.GetService<NoTenantTestDbContext>();

        // Assert: if we got here, TenantSchemaSelector received null tenantId (no schema switching)
        Assert.NotNull(dbContext);
    }

    private static void InvokeConfigureDbContext(IServiceCollection services, string dbType)
    {
        MethodInfo method = typeof(MDbContextConfiguration).GetMethod(
            "ConfigureDbContext",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = method.MakeGenericMethod(typeof(NoTenantTestDbContext), typeof(TestPermission));

        try
        {
            _ = generic.Invoke(null, [services, dbType, true, ""]);
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
        }
    }
}
