namespace Muonroi.AspNetCore.Tests.Permissions;

public class PermissionSyncServiceTests
{
    private class DummyProvider(IEnumerable<PermissionDefinition> defs) : IPermissionProvider
    {
        public IEnumerable<PermissionDefinition> GetPermissions()
        {
            return defs;
        }
    }

    [Fact]
    public void Constructor_Allows_Null()
    {
        PermissionSyncService<TestDbContext> svc = new(null!, null!);
        Assert.NotNull(svc);
    }

    [Fact]
    public void Constructor_Valid_Params()
    {
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("sync_ctor").Options;
        using TestDbContext db = new(opts);
        PermissionSyncService<TestDbContext> svc = new(db, []);
        Assert.NotNull(svc);
    }

    [Fact]
    public async Task SyncPermissionsAsync_Adds_New_Permissions()
    {
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("sync_success").Options;
        using TestDbContext db = new(opts);
        PermissionDefinition def = new()
        {
            GroupName = "grp",
            GroupDisplayName = "grp",
            Permissions = ["Create", "Read"]
        };
        IPermissionProvider provider = new DummyProvider([def]);
        PermissionSyncService<TestDbContext> svc = new(db, [provider]);

        await svc.SyncPermissionsAsync();

        Assert.NotNull(await db.PermissionGroups.FirstOrDefaultAsync(g => g.Name == "grp"));
        Assert.Equal(1, await db.Permissions.CountAsync(p => p.Name == "Create"));
        Assert.Equal(1, await db.Permissions.CountAsync(p => p.Name == "Read"));
    }

    [Fact]
    public async Task SyncPermissionsAsync_Null_Providers_Throws()
    {
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("sync_null").Options;
        using TestDbContext db = new(opts);
        PermissionSyncService<TestDbContext> svc = new(db, null!);
        await Assert.ThrowsAsync<NullReferenceException>(() => svc.SyncPermissionsAsync());
    }

    [Fact]
    public async Task SyncPermissionsAsync_Permission_List_Changed()
    {
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("sync_changed").Options;
        using TestDbContext db = new(opts);
        MPermission entity = new()
        {
            Name = "Read",
            UiKey = string.Empty
        };
        db.Permissions.Add(entity);
        db.SaveChanges();
        PermissionDefinition def = new()
        {
            GroupName = "grp",
            GroupDisplayName = "grp",
            Permissions = ["Create", "Read", "Write"]
        };
        IPermissionProvider provider = new DummyProvider([def]);
        PermissionSyncService<TestDbContext> svc = new(db, [provider]);

        await svc.SyncPermissionsAsync();

        Assert.Equal(1, await db.PermissionGroups.CountAsync(g => g.Name == "grp"));
        Assert.Equal(1, await db.Permissions.CountAsync(p => p.Name == "Create"));
        Assert.Equal(1, await db.Permissions.CountAsync(p => p.Name == "Read"));
        Assert.Equal(1, await db.Permissions.CountAsync(p => p.Name == "Write"));
        MPermission read = await db.Permissions.FirstAsync(p => p.Name == "Read");
        Assert.Equal("Read", read.UiKey);
        Assert.Equal(PermissionType.Action, read.Type);
    }

    [Fact]
    public async Task SyncPermissionsAsync_No_New_Permissions()
    {
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("sync_nonew").Options;
        using TestDbContext db = new(opts);
        PermissionDefinition def = new()
        {
            GroupName = "grp",
            GroupDisplayName = "grp",
            Permissions = ["Create", "Read"]
        };
        IPermissionProvider provider = new DummyProvider([def]);
        PermissionSyncService<TestDbContext> svc = new(db, [provider]);

        await svc.SyncPermissionsAsync();
        int permCount = await db.Permissions.CountAsync();
        int groupCount = await db.PermissionGroups.CountAsync();

        await svc.SyncPermissionsAsync();

        Assert.Equal(permCount, await db.Permissions.CountAsync());
        Assert.Equal(groupCount, await db.PermissionGroups.CountAsync());
    }

    [Fact]
    public async Task SyncPermissionsAsync_Save_Fails()
    {
        DbContextOptions<FaultyDbContext> opts = new DbContextOptionsBuilder<FaultyDbContext>()
            .UseInMemoryDatabase("sync_error").Options;
        using FaultyDbContext db = new(opts);
        PermissionDefinition def = new()
        {
            GroupName = "grp",
            GroupDisplayName = "grp",
            Permissions = ["Create"]
        };
        IPermissionProvider provider = new DummyProvider([def]);
        PermissionSyncService<FaultyDbContext> svc = new(db, [provider]);

        await Assert.ThrowsAsync<Exception>(() => svc.SyncPermissionsAsync());
    }
}
