namespace Muonroi.BuildingBlock.Test;

public class SharedDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_Returns_Context_When_Config_Valid()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string json =
            "{\"EnableEncryption\":false,\"DatabaseConfigs\":{\"DbType\":\"Sqlite\",\"ConnectionStrings\":{\"SqliteConnectionString\":\"Data Source=test.db\"}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        using TestDbContext context = factory.CreateDbContext([]);
        Assert.NotNull(context);

        Directory.SetCurrentDirectory(original);
    }

    [Fact]
    public void CreateDbContext_Throws_When_Config_Missing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), "{}");
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));

        Directory.SetCurrentDirectory(original);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", true)]
    public void CreateDbContext_Handles_Invalid_ConnectionString(string? cs, bool succeed)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string json = cs is null
            ? "{\"EnableEncryption\":false,\"DatabaseConfigs\":{\"DbType\":\"Sqlite\",\"ConnectionStrings\":{}}}"
            : $"{{\"EnableEncryption\":false,\"DatabaseConfigs\":{{\"DbType\":\"Sqlite\",\"ConnectionStrings\":{{\"SqliteConnectionString\":\"{cs}\"}}}}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        if (succeed)
        {
            using TestDbContext context = factory.CreateDbContext([]);
            Assert.NotNull(context);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));
        }

        Directory.SetCurrentDirectory(original);
    }

    [Fact]
    public void CreateDbContext_Throws_When_Context_Ctor_Invalid()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string json =
            "{\"EnableEncryption\":false,\"DatabaseConfigs\":{\"DbType\":\"Sqlite\",\"ConnectionStrings\":{\"SqliteConnectionString\":\"Data Source=test.db\"}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<NoCtorDbContext> factory = new();
        Assert.ThrowsAny<Exception>(() => factory.CreateDbContext([]));

        Directory.SetCurrentDirectory(original);
    }

    private class NoCtorDbContext()
        : MDbContext(new DbContextOptionsBuilder<NoCtorDbContext>().UseInMemoryDatabase("none").Options,
            new FakeMediator());


    [Fact]
    public void CreateDbContext_Uses_Base_Config_When_Env_Not_Set()
    {
        string? oldEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);

        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string json =
            "{\"EnableEncryption\":false,\"DatabaseConfigs\":{\"DbType\":\"Sqlite\",\"ConnectionStrings\":{\"SqliteConnectionString\":\"Data Source=base.db\"}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        using TestDbContext context = factory.CreateDbContext([]);
        Assert.Contains("base.db", context.Database.GetDbConnection().ConnectionString);

        Directory.SetCurrentDirectory(original);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", oldEnv);
    }

    [Fact]
    public void CreateDbContext_Uses_Env_Specific_Config()
    {
        string? oldEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "EnvTest");

        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string baseJson =
            "{\"EnableEncryption\":false,\"DatabaseConfigs\":{\"DbType\":\"Sqlite\",\"ConnectionStrings\":{\"SqliteConnectionString\":\"Data Source=base.db\"}}}";
        string envJson =
            "{\"DatabaseConfigs\":{\"ConnectionStrings\":{\"SqliteConnectionString\":\"Data Source=env.db\"}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), baseJson);
        File.WriteAllText(Path.Combine(tempDir, "appsettings.EnvTest.json"), envJson);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        using TestDbContext context = factory.CreateDbContext([]);
        Assert.Contains("env.db", context.Database.GetDbConnection().ConnectionString);

        Directory.SetCurrentDirectory(original);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", oldEnv);
    }

    [Theory]
    [InlineData(nameof(DbTypes.SqlServer), "SqlServerConnectionString",
        "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=True;TrustServerCertificate=True;")]
    [InlineData(nameof(DbTypes.PostgreSql), "PostgreSqlConnectionString",
        "Host=localhost;Database=test;Username=test;Password=test")]
    [InlineData(nameof(DbTypes.Sqlite), "SqliteConnectionString", "Data Source=test.db")]
    public void CreateDbContext_Works_For_Supported_Providers(string dbType, string key, string cs)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string escCs = cs.Replace("\\", "\\\\");
        string json =
            $"{{\"EnableEncryption\":false,\"DatabaseConfigs\":{{\"DbType\":\"{dbType}\",\"ConnectionStrings\":{{\"{key}\":\"{escCs}\"}}}}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        using TestDbContext context = factory.CreateDbContext([]);
        Assert.NotNull(context);

        Directory.SetCurrentDirectory(original);
    }

    [Fact]
    public void CreateDbContext_Throws_When_Unsupported_DbType()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string json = "{\"EnableEncryption\":false,\"DatabaseConfigs\":{\"DbType\":\"MongoDb\",\"ConnectionStrings\":{}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));

        Directory.SetCurrentDirectory(original);
    }

    [Theory]
    [MemberData(nameof(ArgsData))]
    public void CreateDbContext_Ignores_Args(string[]? args)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string json =
            "{\"EnableEncryption\":false,\"DatabaseConfigs\":{\"DbType\":\"Sqlite\",\"ConnectionStrings\":{\"SqliteConnectionString\":\"Data Source=test.db\"}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        using TestDbContext context = factory.CreateDbContext(args ?? []);
        Assert.NotNull(context);

        Directory.SetCurrentDirectory(original);
    }

    public static TheoryData<string[]?> ArgsData()
    {
        TheoryData<string[]?> data = [null, [], ["one", "two"]];
        return data;
    }

    [Fact]
    public void CreateDbContext_Uses_Base_When_Env_File_Missing()
    {
        string? oldEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "MissingEnv");

        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string json =
            "{\"EnableEncryption\":false,\"DatabaseConfigs\":{\"DbType\":\"Sqlite\",\"ConnectionStrings\":{\"SqliteConnectionString\":\"Data Source=base.db\"}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        using TestDbContext context = factory.CreateDbContext([]);
        Assert.Contains("base.db", context.Database.GetDbConnection().ConnectionString);

        Directory.SetCurrentDirectory(original);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", oldEnv);
    }

    [Fact]
    public void CreateDbContext_Throws_When_ConnectionString_Key_Invalid()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string json =
            "{\"EnableEncryption\":false,\"DatabaseConfigs\":{\"DbType\":\"SqlServer\",\"ConnectionStrings\":{\"WrongKey\":\"Server=.;Database=test;\"}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));

        Directory.SetCurrentDirectory(original);
    }

    [Fact]
    public void CreateDbContext_Decrypts_Encrypted_ConnectionString()
    {
        string secret = "secret";
        string plainCs = "Data Source=enc.db";
        string cipher = MCryptographyExtension.Encrypt(secret, plainCs);
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string json = "{\"EnableEncryption\":true,\"SecretKey\":\"" + secret +
                   "\",\"DatabaseConfigs\":{\"DbType\":\"Sqlite\",\"ConnectionStrings\":{\"SqliteConnectionString\":\"" +
                   cipher + "\"}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        using TestDbContext context = factory.CreateDbContext([]);
        string usedCs = context.Database.GetDbConnection().ConnectionString;
        Assert.Contains("enc.db", usedCs);
        Assert.NotEqual(cipher, usedCs);

        Directory.SetCurrentDirectory(original);
    }

    [Fact]
    public void CreateDbContext_Works_For_MySql()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string cs = "server=localhost;user=root;database=test;password=123;";
        string json =
            $"{{\"EnableEncryption\":false,\"DatabaseConfigs\":{{\"DbType\":\"MySql\",\"ConnectionStrings\":{{\"MySqlConnectionString\":\"{cs}\"}}}}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        try
        {
            using TestDbContext context = factory.CreateDbContext([]);
            Assert.NotNull(context);
        }
        catch (MySqlConnector.MySqlException ex)
        {
            Assert.True(
                ex.Message.Contains("Access denied", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("Unable to connect", StringComparison.OrdinalIgnoreCase));
        }

        Directory.SetCurrentDirectory(original);
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateDbContext_Throws_When_MySql_ConnectionString_Missing(string? cs)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string csPart = cs is null ? "" : $"\"MySqlConnectionString\":\"{cs}\"";
        string json =
            $"{{\"EnableEncryption\":false,\"DatabaseConfigs\":{{\"DbType\":\"MySql\",\"ConnectionStrings\":{{{csPart}}}}}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        Exception ex = Record.Exception(() => factory.CreateDbContext([]));
        Assert.True(ex is InvalidOperationException || ex is MySqlConnector.MySqlException);

        Directory.SetCurrentDirectory(original);
    }

    [Fact]
    public void CreateDbContext_Throws_When_Unsupported_DbType_In_Builder()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        string json =
            "{\"EnableEncryption\":false,\"DatabaseConfigs\":{\"DbType\":\"Oracle\",\"ConnectionStrings\":{\"OracleConnectionString\":\"Data Source=test_oracle.db\"}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));
        Assert.Contains("Unsupported database type", ex.Message);

        Directory.SetCurrentDirectory(original);
    }

    [Theory]
    [InlineData("Oracle")]
    [InlineData("MongoDb")]
    [InlineData("NoDb")]
    public void CreateDbContext_Throws_If_DbType_Not_Supported(string dbType)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string json =
            $"{{\"EnableEncryption\":false,\"DatabaseConfigs\":{{\"DbType\":\"{dbType}\",\"ConnectionStrings\":{{}}}}}}";
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);
        string original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        SharedDbContextFactory<TestDbContext> factory = new();
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));
        Assert.Contains("Unsupported database type", ex.Message);

        Directory.SetCurrentDirectory(original);
    }
}
