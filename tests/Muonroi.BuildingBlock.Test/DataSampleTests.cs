namespace Muonroi.BuildingBlock.Test;

public class HostRoleAndUserCreatorTests
{
    private static HostRoleAndUserCreator<TestDbContext> CreateCreator(DbContextOptions<TestDbContext> opts,
        out TestDbContext db)
    {
        db = new TestDbContext(opts);
        return new HostRoleAndUserCreator<TestDbContext>(db);
    }

    private static readonly string[] AuthAllPermissionArray = ["Auth_All"];

    private static DbContextOptions<TestDbContext> CreateOptions(string name)
    {
        return new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(name).Options;
    }

    [Fact]
    public void Create_Creates_Admin_User_And_Role()
    {
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("host_create").Options;
        HostRoleAndUserCreator<TestDbContext> creator = CreateCreator(opts, out TestDbContext? db);
        creator.Create();
        Assert.Equal(1, db.Users.Count());
        Assert.Equal(1, db.Set<MRole>().Count());
        Assert.Equal(1, db.Set<MPermission>().Count());
        Assert.Equal(1, db.Set<MRolePermission>().Count());
        Assert.Equal(1, db.Set<MUserRole>().Count());
    }

    [Fact]
    public void Create_Does_Not_Duplicate()
    {
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("host_duplicate").Options;
        HostRoleAndUserCreator<TestDbContext> creator = CreateCreator(opts, out TestDbContext? db);
        creator.Create();
        creator.Create();
        Assert.Equal(1, db.Users.Count());
        Assert.Equal(1, db.Set<MRole>().Count());
        Assert.Equal(1, db.Set<MPermission>().Count());
        Assert.Equal(1, db.Set<MRolePermission>().Count());
        Assert.Equal(1, db.Set<MUserRole>().Count());
    }

    [Fact]
    public void CreateHostRoleAndUsers_Adds_User_When_Missing()
    {
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("host_users").Options;
        HostRoleAndUserCreator<TestDbContext> creator = CreateCreator(opts, out TestDbContext? db);
        MethodInfo mi = typeof(HostRoleAndUserCreator<TestDbContext>)
            .GetMethod("CreateHostRoleAndUsers", BindingFlags.Instance | BindingFlags.NonPublic)!;
        mi.Invoke(creator, null);
        Assert.Single(db.Users);
        mi.Invoke(creator, null);
        Assert.Single(db.Users);
    }

    [Fact]
    public void CreateHostRoleAndUsers_Null_Context_Throws()
    {
        HostRoleAndUserCreator<TestDbContext> creator = new(null!);
        MethodInfo mi = typeof(HostRoleAndUserCreator<TestDbContext>)
            .GetMethod("CreateHostRoleAndUsers", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Throws<TargetInvocationException>(() => mi.Invoke(creator, null));
    }

    [Fact]
    public void CreateDefaultRolesAndPermissions_Creates_When_Empty()
    {
        DbContextOptions<TestDbContext> opt = CreateOptions("create_default");
        using TestDbContext db = new(opt);
        HostRoleAndUserCreator<TestDbContext> creator = new(db);
        MethodInfo mi = typeof(HostRoleAndUserCreator<TestDbContext>).GetMethod("CreateDefaultRolesAndPermissions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        mi.Invoke(creator, null);
        Assert.Equal(1, db.Roles.Count());
        Assert.Equal(1, db.Permissions.Count());
    }

    [Fact]
    public void CreateDefaultRolesAndPermissions_No_Duplicate()
    {
        DbContextOptions<TestDbContext> opt = CreateOptions("create_default_dup");
        using TestDbContext db = new(opt);
        HostRoleAndUserCreator<TestDbContext> creator = new(db);
        MethodInfo mi = typeof(HostRoleAndUserCreator<TestDbContext>).GetMethod("CreateDefaultRolesAndPermissions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        mi.Invoke(creator, null);
        mi.Invoke(creator, null);
        Assert.Equal(1, db.Roles.Count());
        Assert.Equal(1, db.Permissions.Count());
    }

    [Fact]
    public void CreateDefaultRolesAndPermissions_Null_Context_Throws()
    {
        HostRoleAndUserCreator<TestDbContext> creator = new(null!);
        MethodInfo mi = typeof(HostRoleAndUserCreator<TestDbContext>).GetMethod("CreateDefaultRolesAndPermissions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Throws<TargetInvocationException>(() => mi.Invoke(creator, null));
    }

    [Fact]
    public void AssignPermissionsToRoles_Assigns_When_Exists()
    {
        DbContextOptions<TestDbContext> opt = CreateOptions("assign_perm");
        using TestDbContext db = new(opt);
        MRole role = new()
        {
            Name = "Admin",
            DisplayName = "Admin",
            NormalizedName = "ADMIN"
        };
        db.Roles.Add(role);
        MPermission entity = new()
        {
            Name = "Auth_All",
            IsGranted = true,
            UiKey = "Auth_All",
            Type = PermissionType.Action
        };
        db.Permissions.Add(entity);
        MUser user = new()
        {
            UserName = "admin",
            EmailAddress = "a@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        db.Users.Add(user);
        db.SaveChanges();
        HostRoleAndUserCreator<TestDbContext> creator = new(db);
        MethodInfo mi = typeof(HostRoleAndUserCreator<TestDbContext>).GetMethod("AssignPermissionsToRoles",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        mi.Invoke(creator, [AuthAllPermissionArray]);
        Assert.Single(db.RolePermissions);
        Assert.Single(db.UserRoles);
    }

    [Fact]
    public void AssignPermissionsToRoles_No_RoleOrPermission_DoesNothing()
    {
        DbContextOptions<TestDbContext> opt = CreateOptions("assign_none");
        using TestDbContext db = new(opt);
        HostRoleAndUserCreator<TestDbContext> creator = new(db);
        MethodInfo mi = typeof(HostRoleAndUserCreator<TestDbContext>).GetMethod("AssignPermissionsToRoles",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        mi.Invoke(creator, [AuthAllPermissionArray]);
        Assert.Empty(db.RolePermissions);
    }

    [Fact]
    public void AssignPermissionsToRoles_Null_List_Throws()
    {
        DbContextOptions<TestDbContext> opt = CreateOptions("assign_null");
        using TestDbContext db = new(opt);
        MPermission entity = new()
        {
            Name = "Auth_All",
            IsGranted = true,
            UiKey = "Auth_All",
            Type = PermissionType.Action
        };
        db.Permissions.Add(entity);
        db.SaveChanges();
        HostRoleAndUserCreator<TestDbContext> creator = new(db);
        MethodInfo mi = typeof(HostRoleAndUserCreator<TestDbContext>).GetMethod("AssignPermissionsToRoles",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Throws<TargetInvocationException>(() => mi.Invoke(creator, [null!]));
    }
}

public class InitialHostDbBuilderTests
{
    private static DbContextOptions<TestDbContext> CreateOptions(string name)
    {
        return new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(name).Options;
    }

    [Fact]
    public void Create_Builds_Data()
    {
        DbContextOptions<TestDbContext> opt = CreateOptions("init_create");
        using TestDbContext db = new(opt);
        InitialHostDbBuilder<TestDbContext> builder = new(db);
        builder.Create();
        Assert.NotEmpty(db.Languages);
        Assert.NotEmpty(db.Roles);
    }

    [Fact]
    public void Create_No_Duplicate()
    {
        DbContextOptions<TestDbContext> opt = CreateOptions("init_dup");
        using TestDbContext db = new(opt);
        InitialHostDbBuilder<TestDbContext> builder = new(db);
        builder.Create();
        builder.Create();
        Assert.Equal(2, db.Languages.Count());
        Assert.Equal(1, db.Roles.Count());
    }

    [Fact]
    public void Create_Null_Context_Throws()
    {
        InitialHostDbBuilder<TestDbContext> builder = new(null!);
        Assert.Throws<NullReferenceException>(() => builder.Create());
    }
}

public class MigrationManagerTests
{
    [Fact]
    public void MigrateDatabase_Runs_Successfully()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        conn.Open();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<TestDbContext>(o => o.UseSqlite(conn));
        IPermissionSyncService sync = Substitute.For<IPermissionSyncService>();
        builder.Services.AddSingleton(sync);
        WebApplication app = builder.Build();
        using (IServiceScope scope = app.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreated();
        }

        app.MigrateDatabase<TestDbContext>();
        sync.Received(1).SyncPermissionsAsync();
    }

    [Fact]
    public void MigrateDatabase_Sync_Fails_Throws()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        conn.Open();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<TestDbContext>(o => o.UseSqlite(conn));
        IPermissionSyncService sync = Substitute.For<IPermissionSyncService>();
        sync.SyncPermissionsAsync().Returns(_ => throw new Exception("fail"));
        builder.Services.AddSingleton(sync);
        WebApplication app = builder.Build();
        using (IServiceScope scope = app.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreated();
        }

        app.MigrateDatabase<TestDbContext>();
        sync.Received(1).SyncPermissionsAsync();
    }

    [Fact]
    public void MigrateDatabase_Null_App_Throws()
    {
        WebApplication? app = null;
        Assert.Throws<NullReferenceException>(() => app!.MigrateDatabase<TestDbContext>());
    }
}

public class PermissionDataMigratorTests
{
    private static DbContextOptions<TestDbContext> CreateOptions(string name)
    {
        return new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(name).Options;
    }

    [Fact]
    public void Migrate_Updates_Permissions()
    {
        DbContextOptions<TestDbContext> opt = CreateOptions("perm_mig");
        using TestDbContext db = new(opt);
        MPermission entity = new()
        {
            Name = "P1",
            IsGranted = true
        };
        db.Permissions.Add(entity);
        db.SaveChanges();
        PermissionDataMigrator<TestDbContext> migrator = new(db);
        migrator.Migrate();
        MPermission perm = db.Permissions.First();
        Assert.Equal("P1", perm.UiKey);
        Assert.Equal(PermissionType.Action, perm.Type);
    }

    [Fact]
    public void Migrate_Save_Fails_Throws()
    {
        const string dbName = "perm_fail";
        DbContextOptions<TestDbContext> seedOpt = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(dbName).Options;
        using (TestDbContext seed = new(seedOpt))
        {
            MPermission entity = new()
            {
                Name = "P1"
            };
            seed.Permissions.Add(entity);
            seed.SaveChanges();
        }

        DbContextOptions<FaultyDbContext> opt = new DbContextOptionsBuilder<FaultyDbContext>().UseInMemoryDatabase(dbName).Options;
        using FaultyDbContext db = new(opt);
        PermissionDataMigrator<FaultyDbContext> migrator = new(db);
        Assert.Throws<Exception>(() => migrator.Migrate());
    }

    [Fact]
    public void Migrate_Null_Context_Throws()
    {
        PermissionDataMigrator<TestDbContext> migrator = new(null!);
        Assert.Throws<NullReferenceException>(() => migrator.Migrate());
    }
}

public class MongoDbContextConfiguratorTests
{
    [Fact]
    public void Configure_Throws_NotSupported()
    {
        MongoDbContextConfigurator<TestDbContext> cfg = new();
        Assert.Throws<NotSupportedException>(() => cfg.Configure(new DbContextOptionsBuilder<TestDbContext>(), "c"));
    }

    [Fact]
    public void ConfigureMongoDb_Registers_Client()
    {
        MongoDbContextConfigurator<TestDbContext> cfg = new();
        Dictionary<string, string?> data = new()
        {
            ["ConnectionStrings:MongoDbConnectionString"] = "mongodb://localhost",
            ["DatabaseConfigs:DatabaseName"] = "db"
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        ServiceCollection services = [];
        cfg.ConfigureMongoDb(services, configuration);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IMongoClient>());
    }

    [Fact]
    public void ConfigureMongoDb_Null_Config_Throws()
    {
        MongoDbContextConfigurator<TestDbContext> cfg = new();
        Assert.Throws<InvalidDataException>(() => cfg.ConfigureMongoDb(new ServiceCollection(), null!));
    }

    [Fact]
    public void ConfigureMongoDb_Missing_Settings_Throws()
    {
        MongoDbContextConfigurator<TestDbContext> cfg = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        Assert.Throws<InvalidDataException>(() => cfg.ConfigureMongoDb(new ServiceCollection(), configuration));
    }
}
