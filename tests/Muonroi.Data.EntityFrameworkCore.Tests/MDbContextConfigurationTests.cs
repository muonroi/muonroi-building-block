using Muonroi.Governance.Abstractions.License;
using Muonroi.Logging.Abstractions;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Helpers;

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
        IMLog<MDbContext>? logger = null)
        : MDbContext(options, mediator, licenseGuard, logger, new MDateTimeService())
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

    // Auth repositories are registered UNCONDITIONALLY by SystemDependencyInjectionService so consumer
    // apps get ecosystem auth without manual wiring. MAuthenticateTokenHelper's dependencies
    // (ITokenSigner, MTokenInfo) resolve at request time, and IPasswordHasher has a safe fallback
    // (NotConfiguredPasswordHasher), so ValidateOnBuild stays green even when Muonroi.Auth is not set up.
    [Fact]
    public void SystemDependencyInjectionService_RegistersAuthRepositories()
    {
        ServiceCollection services = [];

        _ = InvokeSystemDi(services);

        Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IAuthenticateRepository)));
        Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IRefreshTokenValidator)));
    }

    // Registration is idempotent — repeated calls register each auth repository exactly once
    // (TryAddScoped/TryAddSingleton are idempotent).
    [Fact]
    public void SystemDependencyInjectionService_IsIdempotent_RegistersAuthRepositoriesOnce()
    {
        ServiceCollection services = [];

        _ = InvokeSystemDi(services);
        _ = InvokeSystemDi(services);

        Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IAuthenticateRepository)));
        Assert.Equal(1, services.Count(x => x.ServiceType == typeof(IRefreshTokenValidator)));
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

        MConfigurationException exception = Assert.Throws<MConfigurationException>(() => InvokeDecrypt(configs, configuration));
        Assert.Equal("Required configuration 'DatabaseConfigs:ConnectionStrings' is missing or empty.", exception.Message);
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
    public void DecryptConnectionString_Encrypted_WithWrongKey_DoesNotRecoverPlaintext()
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

        // MCryptographyExtension uses unauthenticated AES-CBC + PKCS7. A wrong key usually throws
        // (invalid padding on the final block), but ~1/256 of random IVs decrypt to coincidentally
        // valid padding and return garbage WITHOUT throwing. Asserting "throws" was therefore a
        // ~0.4% flake. The deterministic security invariant is: a wrong key must never recover the
        // original plaintext — it either throws or yields a value that differs from it.
        string recovered;
        try
        {
            recovered = InvokeDecrypt(configs, configuration);
        }
        catch (Exception)
        {
            // Threw on invalid padding — wrong key rejected, invariant satisfied.
            return;
        }

        Assert.NotEqual(plain, recovered);
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
        MInternalException exception = Assert.Throws<MInternalException>(() => InvokeConfigure(services, "Invalid"));
        Assert.Equal("Unsupported database type: Invalid", exception.Message);
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
