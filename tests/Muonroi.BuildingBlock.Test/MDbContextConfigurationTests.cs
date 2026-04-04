namespace Muonroi.BuildingBlock.Test;

using Muonroi.Governance.License;
using Muonroi.Core.Abstractions.Exceptions;

public class MDbContextConfigurationTests
{
    private static IServiceCollection InvokeSystemDi(IServiceCollection services)
    {
        MethodInfo mi = typeof(MDbContextConfiguration).GetMethod("SystemDependencyInjectionService",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = mi.MakeGenericMethod(typeof(TestDbContext), typeof(TestPerm));
        try
        {
            generic.Invoke(null, [services]);
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
        }

        return services;
    }

    private static string InvokeDecrypt(DatabaseConfigs cfg, IConfiguration config, bool useDefault = true,
        string key = "", string fingerprint = "")
    {
        MethodInfo mi = typeof(MDbContextConfiguration).GetMethod("DecryptConnectionString",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            return (string)mi.Invoke(null, [cfg, config, useDefault, key, fingerprint])!;
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
            return string.Empty;
        }
    }

    private static void InvokeConfigure(IServiceCollection services, string dbType, bool isSecretDefault = true,
        string secretKey = "")
    {
        MethodInfo mi = typeof(MDbContextConfiguration).GetMethod("ConfigureDbContext",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = mi.MakeGenericMethod(typeof(TestDbContext), typeof(TestPerm));
        try
        {
            generic.Invoke(null, [services, dbType, isSecretDefault, secretKey]);
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
        }
    }

    [Fact]
    public void SystemDependencyInjectionService_Registers_Scoped_Services()
    {
        ServiceCollection services = [];
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase("di"));
        services.AddScoped<MDbContext, TestDbContext>();
        services.AddScoped(_ => new MAuthenticateInfoContext(false));
        InvokeSystemDi(services);
        InvokeSystemDi(services);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IAuthService<TestPerm, TestDbContext>)));
        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IAuthenticateRepository)));
        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IPermissionService<TestPerm>)));
    }

    [Fact]
    public void SystemDependencyInjectionService_Null_Services_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<MArgumentException>(() => InvokeSystemDi(services!));
    }

    [Fact]
    public void DecryptConnectionString_Returns_String_When_Not_Encrypted()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EnableEncryption"] = "false"
        }).Build();
        DatabaseConfigs cfg = new()
        {
            DbType = nameof(DbTypes.PostgreSql),
            ConnectionStrings = new ConnectionStrings
            {
                PostgreSqlConnectionString = "Host=db;"
            }
        };
        string result = InvokeDecrypt(cfg, config);
        Assert.Equal("Host=db;", result);
    }

    [Fact]
    public void DecryptConnectionString_Null_Value_Throws()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EnableEncryption"] = "false"
        }).Build();
        DatabaseConfigs cfg = new()
        {
            DbType = nameof(DbTypes.PostgreSql),
            ConnectionStrings = new ConnectionStrings()
        };
        Assert.Throws<InvalidDataException>(() => InvokeDecrypt(cfg, config));
    }

    [Fact]
    public void DecryptConnectionString_Wrong_Key_Throws()
    {
        string secret = "secret";
        string cipher = MCryptographyExtension.Encrypt(secret, "Conn");
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EnableEncryption"] = "true",
            ["SecretKey"] = "bad"
        }).Build();
        DatabaseConfigs cfg = new()
        {
            DbType = nameof(DbTypes.PostgreSql),
            ConnectionStrings = new ConnectionStrings
            {
                PostgreSqlConnectionString = cipher
            }
        };
        string result = InvokeDecrypt(cfg, config);
        Assert.NotEqual("Conn", result);
    }

    [Fact]
    public void DecryptConnectionString_Valid_Encrypted_String_Returns_Plain()
    {
        string secret = "secret";
        string plain = "Host=db;";
        string cipher = MCryptographyExtension.Encrypt(secret, plain);
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EnableEncryption"] = "true",
            ["SecretKey"] = secret
        }).Build();
        DatabaseConfigs cfg = new()
        {
            DbType = nameof(DbTypes.PostgreSql),
            ConnectionStrings = new ConnectionStrings
            {
                PostgreSqlConnectionString = cipher
            }
        };
        string result = InvokeDecrypt(cfg, config);
        Assert.Equal(plain, result);
    }

    [Fact]
    public void DecryptConnectionString_Invalid_Cipher_Throws()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EnableEncryption"] = "true",
            ["SecretKey"] = "secret"
        }).Build();
        DatabaseConfigs cfg = new()
        {
            DbType = nameof(DbTypes.PostgreSql),
            ConnectionStrings = new ConnectionStrings
            {
                PostgreSqlConnectionString = "invalid"
            }
        };
        string result = InvokeDecrypt(cfg, config);
        Assert.NotEqual("invalid", result);
    }

    [Fact]
    public void DecryptConnectionString_Empty_String_Returns_Empty()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EnableEncryption"] = "false"
        }).Build();
        DatabaseConfigs cfg = new()
        {
            DbType = nameof(DbTypes.PostgreSql),
            ConnectionStrings = new ConnectionStrings
            {
                PostgreSqlConnectionString = string.Empty
            }
        };
        string result = InvokeDecrypt(cfg, config);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ConfigureDbContext_Registers_DbContext()
    {
        Dictionary<string, string?> data = new()
        {
            ["DatabaseConfigs:DbType"] = nameof(DbTypes.Sqlite),
            ["DatabaseConfigs:ConnectionStrings:SqliteConnectionString"] = "DataSource=:memory:",
            ["EnableEncryption"] = "false"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        ServiceCollection services = [];
        services.AddSingleton(config);
        services.AddSingleton<ITenantContext, TenantContext>();
        services.AddSingleton<ITenantConnectionStringFactory>(
            new DefaultTenantConnectionStringFactory("DataSource=:memory:"));
        services.AddSingleton<ILicenseGuard>(new TestLicenseGuard());
        InvokeConfigure(services, nameof(DbTypes.Sqlite));
        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        TestDbContext ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        Assert.NotNull(ctx);
    }

    [Fact]
    public void ConfigureDbContext_Invalid_DbType_Throws()
    {
        ServiceCollection services = [];
        Assert.Throws<MArgumentException>(() => InvokeConfigure(services, "Invalid"));
    }

    [Fact]
    public void ConfigureDbContext_Missing_Dependency_Throws_On_Resolve()
    {
        Dictionary<string, string?> data = new()
        {
            ["DatabaseConfigs:DbType"] = nameof(DbTypes.Sqlite),
            ["DatabaseConfigs:ConnectionStrings:SqliteConnectionString"] = "DataSource=:memory:",
            ["EnableEncryption"] = "false"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        ServiceCollection services = [];
        services.AddSingleton(config);
        services.AddSingleton<ITenantConnectionStringFactory>(
            new DefaultTenantConnectionStringFactory("DataSource=:memory:"));
        services.AddSingleton<ILicenseGuard>(new TestLicenseGuard());
        InvokeConfigure(services, nameof(DbTypes.Sqlite));
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.Throws<MInternalException>(() => provider.GetRequiredService<TestDbContext>());
    }

    [Fact]
    public void ConfigureDbContext_Null_ConnectionString_Throws_On_Resolve()
    {
        Dictionary<string, string?> data = new()
        {
            ["DatabaseConfigs:DbType"] = nameof(DbTypes.Sqlite),
            ["DatabaseConfigs:ConnectionStrings:SqliteConnectionString"] = "",
            ["EnableEncryption"] = "false"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        ServiceCollection services = [];
        services.AddSingleton(config);
        services.AddSingleton<ITenantContext, TenantContext>();
        services.AddSingleton<ITenantConnectionStringFactory>(new DefaultTenantConnectionStringFactory(null!));
        services.AddSingleton<ILicenseGuard>(new TestLicenseGuard());
        InvokeConfigure(services, nameof(DbTypes.Sqlite));
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.ThrowsAny<Exception>(() => provider.GetRequiredService<TestDbContext>());
    }
}
