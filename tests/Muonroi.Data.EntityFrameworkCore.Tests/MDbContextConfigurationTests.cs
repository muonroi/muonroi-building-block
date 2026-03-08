using Muonroi.Logging.Abstractions;

namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class MDbContextConfigurationTests
{
    private enum TestPermission
    {
        Read
    }

    private sealed class ConfigTestDbContext(
        DbContextOptions<ConfigTestDbContext> options,
        IMediator mediator,
        ILicenseGuard? licenseGuard = null,
        IMLog<ConfigTestDbContext>? logger = null)
        : MDbContext(options, mediator, licenseGuard, null, new MDateTimeService())
    {
    }

    private static IServiceCollection InvokeSystemDi(IServiceCollection services)
    {
        MethodInfo method = typeof(MDbContextConfiguration).GetMethod(
            "SystemDependencyInjectionService",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        MethodInfo generic = method.MakeGenericMethod(typeof(ConfigTestDbContext), typeof(TestPermission));
        try
        {
            _ = generic.Invoke(null, [services]);
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
        }

        return services;
    }

    private static string InvokeDecrypt(
        DatabaseConfigs configs,
        IConfiguration configuration,
        bool useDefault = true,
        string key = "",
        string fingerprint = "")
    {
        MethodInfo method = typeof(MDbContextConfiguration).GetMethod(
            "DecryptConnectionString",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            return (string)method.Invoke(null, [configs, configuration, useDefault, key, fingerprint])!;
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
            return string.Empty;
        }
    }

    private static void InvokeConfigure(
        IServiceCollection services,
        string dbType,
        bool isSecretDefault = true,
        string secretKey = "")
    {
        MethodInfo method = typeof(MDbContextConfiguration).GetMethod(
            "ConfigureDbContext",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = method.MakeGenericMethod(typeof(ConfigTestDbContext), typeof(TestPermission));

        try
        {
            _ = generic.Invoke(null, [services, dbType, isSecretDefault, secretKey]);
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
        }
    }

    [Fact]
    public void SystemDependencyInjectionService_Registers_Expected_Services()
    {
        ServiceCollection services = [];

        _ = InvokeSystemDi(services);
        _ = InvokeSystemDi(services);

        Assert.Equal(2, services.Count(x => x.ServiceType == typeof(IAuthenticateRepository)));
        Assert.Equal(2, services.Count(x => x.ServiceType == typeof(IRefreshTokenValidator)));
    }

    [Fact]
    public void SystemDependencyInjectionService_NullServices_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => InvokeSystemDi(services!));
    }

    [Fact]
    public void DecryptConnectionString_Returns_Plain_When_Encryption_Disabled()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "false"
            })
            .Build();

        DatabaseConfigs configs = new()
        {
            DbType = nameof(DbTypes.PostgreSql),
            ConnectionStrings = new ConnectionStrings
            {
                PostgreSqlConnectionString = "Host=db;"
            }
        };

        string value = InvokeDecrypt(configs, configuration);

        Assert.Equal("Host=db;", value);
    }

    [Fact]
    public void DecryptConnectionString_NullConnectionString_Throws()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "false"
            })
            .Build();

        DatabaseConfigs configs = new()
        {
            DbType = nameof(DbTypes.PostgreSql),
            ConnectionStrings = new ConnectionStrings()
        };

        Assert.Throws<InvalidDataException>(() => InvokeDecrypt(configs, configuration));
    }

    [Fact]
    public void DecryptConnectionString_Encrypted_Returns_Plain_When_Key_Valid()
    {
        const string secret = "secret";
        const string plain = "Data Source=enc.db";
        string cipher = MCryptographyExtension.Encrypt(secret, plain);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "true",
                ["SecretKey"] = secret
            })
            .Build();

        DatabaseConfigs configs = new()
        {
            DbType = nameof(DbTypes.Sqlite),
            ConnectionStrings = new ConnectionStrings
            {
                SqliteConnectionString = cipher
            }
        };

        string value = InvokeDecrypt(configs, configuration);

        Assert.Equal(plain, value);
    }

    [Fact]
    public void DecryptConnectionString_Encrypted_WithWrongKey_Throws()
    {
        const string plain = "Data Source=enc.db";
        string cipher = MCryptographyExtension.Encrypt("correct", plain);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "true",
                ["SecretKey"] = "wrong"
            })
            .Build();

        DatabaseConfigs configs = new()
        {
            DbType = nameof(DbTypes.Sqlite),
            ConnectionStrings = new ConnectionStrings
            {
                SqliteConnectionString = cipher
            }
        };

        Assert.ThrowsAny<Exception>(() => InvokeDecrypt(configs, configuration));
    }

    [Fact]
    public void ConfigureDbContext_Registers_SqliteConfigurator()
    {
        ServiceCollection services = [];

        InvokeConfigure(services, nameof(DbTypes.Sqlite));

        ServiceDescriptor? descriptor = services.FirstOrDefault(x =>
            x.ServiceType == typeof(IDbContextConfigurator<ConfigTestDbContext>));

        Assert.NotNull(descriptor);
        Assert.Equal(typeof(SqliteDbContextConfigurator<ConfigTestDbContext>), descriptor.ImplementationType);
    }

    [Fact]
    public void ConfigureDbContext_Invalid_DbType_Throws()
    {
        ServiceCollection services = [];
        Assert.Throws<ArgumentException>(() => InvokeConfigure(services, "Invalid"));
    }

    [Fact]
    public void AddDbContextConfigure_Sqlite_Resolves_DbContext()
    {
        Dictionary<string, string?> data = new()
        {
            ["DatabaseConfigs:DbType"] = nameof(DbTypes.Sqlite),
            ["DatabaseConfigs:ConnectionStrings:SqliteConnectionString"] = "DataSource=:memory:",
            ["EnableEncryption"] = "false"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();

        ServiceCollection services = [];
        _ = services.AddSingleton(configuration);
        _ = services.AddSingleton<ILicenseGuard>(new TestLicenseGuard());
        _ = services.AddSingleton(new LicenseConfigs());
        _ = services.AddScoped<IMediator>(_ => new NoMediator());
        _ = services.AddLogging();

        _ = services.AddDbContextConfigure<ConfigTestDbContext, TestPermission>(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        ConfigTestDbContext context = scope.ServiceProvider.GetRequiredService<ConfigTestDbContext>();
        Assert.NotNull(context);
    }
}
