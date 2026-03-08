using Muonroi.Logging.Abstractions;

namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class SharedDbContextFactoryTests
{
    private sealed class FactoryTestDbContext(
        DbContextOptions<FactoryTestDbContext> options,
        IMediator mediator,
        ILicenseGuard? licenseGuard = null,
        IMLog<FactoryTestDbContext>? logger = null)
        : MDbContext(options, mediator, licenseGuard, null, new MDateTimeService())
    {
    }

    private sealed class NoCtorDbContext()
        : MDbContext(
            new DbContextOptionsBuilder<NoCtorDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
            new NoMediator(),
            new TestLicenseGuard(),
            null,
            new MDateTimeService())
    {
    }

    [Fact]
    public void CreateDbContext_ValidConfig_Returns_Context()
    {
        const string json = """
                            {
                              "EnableEncryption": false,
                              "DatabaseConfigs": {
                                "DbType": "Sqlite",
                                "ConnectionStrings": { "SqliteConnectionString": "Data Source=test.db" }
                              }
                            }
                            """;

        RunInTempConfigDirectory(
            json,
            () =>
            {
                SharedDbContextFactory<FactoryTestDbContext> factory = new();
                using FactoryTestDbContext context = factory.CreateDbContext([]);
                Assert.NotNull(context);
            });
    }

    [Fact]
    public void CreateDbContext_MissingConfig_Throws()
    {
        RunInTempConfigDirectory(
            "{}",
            () =>
            {
                SharedDbContextFactory<FactoryTestDbContext> factory = new();
                Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));
            });
    }

    [Fact]
    public void CreateDbContext_Uses_EnvironmentSpecific_Config()
    {
        const string baseJson = """
                                {
                                  "EnableEncryption": false,
                                  "DatabaseConfigs": {
                                    "DbType": "Sqlite",
                                    "ConnectionStrings": { "SqliteConnectionString": "Data Source=base.db" }
                                  }
                                }
                                """;

        const string envJson = """
                               {
                                 "DatabaseConfigs": {
                                   "ConnectionStrings": { "SqliteConnectionString": "Data Source=env.db" }
                                 }
                               }
                               """;

        RunInTempConfigDirectory(
            baseJson,
            () =>
            {
                SharedDbContextFactory<FactoryTestDbContext> factory = new();
                using FactoryTestDbContext context = factory.CreateDbContext([]);
                Assert.Contains("env.db", context.Database.GetDbConnection().ConnectionString, StringComparison.OrdinalIgnoreCase);
            },
            "EnvTest",
            envJson);
    }

    [Fact]
    public void CreateDbContext_Unsupported_DbType_Throws()
    {
        const string json = """
                            {
                              "EnableEncryption": false,
                              "DatabaseConfigs": {
                                "DbType": "MongoDb",
                                "ConnectionStrings": { }
                              }
                            }
                            """;

        RunInTempConfigDirectory(
            json,
            () =>
            {
                SharedDbContextFactory<FactoryTestDbContext> factory = new();
                Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));
            });
    }

    [Fact]
    public void CreateDbContext_Unsupported_ContextConstructor_Throws()
    {
        const string json = """
                            {
                              "EnableEncryption": false,
                              "DatabaseConfigs": {
                                "DbType": "Sqlite",
                                "ConnectionStrings": { "SqliteConnectionString": "Data Source=test.db" }
                              }
                            }
                            """;

        RunInTempConfigDirectory(
            json,
            () =>
            {
                SharedDbContextFactory<NoCtorDbContext> factory = new();
                Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));
            });
    }

    [Fact]
    public void CreateDbContext_Decrypts_Encrypted_ConnectionString()
    {
        const string secret = "secret";
        const string plain = "Data Source=enc.db";
        string cipher = MCryptographyExtension.Encrypt(secret, plain);
        string json = $$"""
                        {
                          "EnableEncryption": true,
                          "SecretKey": "{{secret}}",
                          "DatabaseConfigs": {
                            "DbType": "Sqlite",
                            "ConnectionStrings": { "SqliteConnectionString": "{{cipher}}" }
                          }
                        }
                        """;

        RunInTempConfigDirectory(
            json,
            () =>
            {
                SharedDbContextFactory<FactoryTestDbContext> factory = new();
                using FactoryTestDbContext context = factory.CreateDbContext([]);
                string used = context.Database.GetDbConnection().ConnectionString;
                Assert.Contains("enc.db", used, StringComparison.OrdinalIgnoreCase);
                Assert.NotEqual(cipher, used);
            });
    }

    [Theory]
    [MemberData(nameof(ArgsData))]
    public void CreateDbContext_Ignores_Args(string[] args)
    {
        const string json = """
                            {
                              "EnableEncryption": false,
                              "DatabaseConfigs": {
                                "DbType": "Sqlite",
                                "ConnectionStrings": { "SqliteConnectionString": "Data Source=test.db" }
                              }
                            }
                            """;

        RunInTempConfigDirectory(
            json,
            () =>
            {
                SharedDbContextFactory<FactoryTestDbContext> factory = new();
                using FactoryTestDbContext context = factory.CreateDbContext(args);
                Assert.NotNull(context);
            });
    }

    public static TheoryData<string[]> ArgsData()
    {
        return
        [
            [],
            ["one", "two"]
        ];
    }

    private static void RunInTempConfigDirectory(
        string appsettingsJson,
        Action action,
        string? environmentName = null,
        string? environmentJson = null)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), appsettingsJson);

        if (!string.IsNullOrEmpty(environmentName) && environmentJson != null)
        {
            File.WriteAllText(Path.Combine(tempDir, $"appsettings.{environmentName}.json"), environmentJson);
        }

        string originalDir = Directory.GetCurrentDirectory();
        string? originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environmentName);
            Directory.SetCurrentDirectory(tempDir);
            action();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
            }
        }
    }
}
