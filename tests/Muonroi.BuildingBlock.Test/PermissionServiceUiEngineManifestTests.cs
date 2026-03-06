using Muonroi.Tenancy;

namespace Muonroi.BuildingBlock.Test;

public class PermissionServiceUiEngineManifestTests
{
    private static PermissionService<TestPerm, TestDbContext> CreateService(
        TestDbContext db,
        MAuthenticateInfoContext ctx,
        ILicenseGuard? guard = null,
        ITenantQuotaStore? quotaStore = null,
        IUiEngineSchemaNotifier? schemaNotifier = null)
    {
        TenantContext.CurrentTenantId ??= Guid.NewGuid().ToString();
        return new PermissionService<TestPerm, TestDbContext>(db, ctx, null, guard, quotaStore, schemaNotifier);
    }

    [Fact]
    public async Task GetUiEngineManifestAsync_Composes_Runtime_Metadata_From_Backend()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using TestDbContext db = new(options);

        MUser user = new()
        {
            UserName = "engine-user",
            EmailAddress = "engine@local",
            Name = "Engine",
            Surname = "User",
            Password = "p"
        };

        MRole role = new()
        {
            Name = "engine-role",
            DisplayName = "Engine role",
            NormalizedName = "ENGINE-ROLE"
        };

        MPermissionGroup group = new()
        {
            Name = "Operations",
            DisplayName = "Operations"
        };

        MPermission menu = new()
        {
            Name = "Ops_Menu",
            UiKey = "ops",
            Type = PermissionType.Menu,
            Label = "Operations",
            IsGranted = true,
            PermissionGroup = group,
            Order = 1
        };

        MPermission action = new()
        {
            Name = "Ops_Task_Run",
            UiKey = "ops.task.run",
            Parent = menu,
            Type = PermissionType.Action,
            Label = "Run task",
            IsGranted = true,
            PermissionGroup = group,
            Order = 2
        };

        await db.Users.AddAsync(user);
        await db.Roles.AddAsync(role);
        await db.PermissionGroups.AddAsync(group);
        await db.Permissions.AddRangeAsync(menu, action);
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
            PermissionId = action.EntityId
        };
        await db.RolePermissions.AddAsync(permission);

        await db.SaveChangesAsync();

        PermissionService<TestPerm, TestDbContext> service = CreateService(db, new MAuthenticateInfoContext(false));
        MResponse<MUiEngineManifest> response = await service.GetUiEngineManifestAsync(user.EntityId, CancellationToken.None);

        MUiEngineManifest manifest = Assert.IsType<MUiEngineManifest>(response.Result);
        Assert.Equal(MUiEngineManifest.MSchemaVersionV1, manifest.SchemaVersion);
        Assert.Equal(user.EntityId, manifest.UserId);
        Assert.Equal(TenantTier.Free.ToString(), manifest.LicenseTier);
        Assert.Equal(3, manifest.Capabilities.Count);
        Assert.Equal(2, manifest.NavigationGroups.Count);
        Assert.Equal(3, manifest.Screens.Count);
        Assert.Equal(5, manifest.Actions.Count);
        Assert.Equal(3, manifest.DataSources.Count);

        MUiEngineScreen screen = Assert.Single(manifest.Screens, x => x.ScreenKey == "screen:ops");
        Assert.Equal("screen:ops", screen.ScreenKey);
        Assert.Equal("/ops", screen.Route);
        Assert.True(screen.IsVisible);
        Assert.Contains("action:ops-task-run", screen.ActionKeys);

        MUiEngineAction actionEntry = Assert.Single(manifest.Actions, x => x.ActionKey == "action:ops-task-run");
        Assert.Equal("action:ops-task-run", actionEntry.ActionKey);
        Assert.Equal("screen:ops", actionEntry.TargetScreenKey);
        Assert.Equal("navigate", actionEntry.ActionType);

        MUiEngineDataSource dataSource = Assert.Single(manifest.DataSources, x => x.DataSourceKey == "datasource:ops");
        Assert.Equal("datasource:ops", dataSource.DataSourceKey);
        Assert.Equal("/api/v1/ops", dataSource.EndpointPath);

        MUiEngineScreen decisionScreen = Assert.Single(manifest.Screens, x => x.ScreenKey == "screen:decision-table-list");
        Assert.Equal("decision-tables", decisionScreen.RequiredCapability);
        Assert.False(decisionScreen.IsEnabled);
        Assert.Equal("DecisionTableUpgradePrompt", Assert.Single(decisionScreen.Components).ComponentType);
    }

    [Fact]
    public async Task GetUiEngineManifestAsync_Uses_AdvancedAuth_License_Guard()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using TestDbContext db = new(options);
        ILicenseGuard guard = Substitute.For<ILicenseGuard>();
        PermissionService<TestPerm, TestDbContext> service = CreateService(db, new MAuthenticateInfoContext(false), guard);

        _ = await service.GetUiEngineManifestAsync(Guid.NewGuid(), CancellationToken.None);

        guard.Received(1).EnsureFeature(FreeTierFeatures.Premium.AdvancedAuth);
    }

    [Fact]
    public async Task GetUiEngineContractInfoAsync_Returns_Runtime_Schema_Version()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using TestDbContext db = new(options);
        PermissionService<TestPerm, TestDbContext> service = CreateService(db, new MAuthenticateInfoContext(false));

        MResponse<MUiEngineContractInfo> response = await service.GetUiEngineContractInfoAsync(CancellationToken.None);
        MUiEngineContractInfo info = Assert.IsType<MUiEngineContractInfo>(response.Result);

        Assert.Equal(MUiEngineManifest.MSchemaVersionV1, info.RuntimeSchemaVersion);
        Assert.Contains(MUiEngineManifest.MSchemaVersionV1, info.SupportedSchemaVersions);
        Assert.Equal("/api/v1/auth/ui-engine/schema-hash", info.SchemaHashEndpoint);
        Assert.Equal("/api/v1/auth/ui-engine/notify-change", info.NotifyChangeEndpoint);
    }

    [Fact]
    public async Task GetUiEngineManifestAsync_Applies_Tier_Based_Capability_Guards()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using TestDbContext db = new(options);
        TenantContext.CurrentTenantId = "tenant-capability";

        MUser user = new()
        {
            UserName = "cap-user",
            EmailAddress = "cap@local",
            Name = "Cap",
            Surname = "User",
            Password = "p"
        };

        MRole role = new()
        {
            Name = "cap-role",
            DisplayName = "Capability role",
            NormalizedName = "CAP-ROLE"
        };

        MPermissionGroup group = new()
        {
            Name = "Rules",
            DisplayName = "Rules"
        };

        MPermission menu = new()
        {
            Name = "Rule_Engine_View",
            UiKey = "rule.engine",
            Type = PermissionType.Menu,
            Label = "Rule Engine",
            IsGranted = true,
            PermissionGroup = group,
            Order = 1
        };

        await db.Users.AddAsync(user);
        await db.Roles.AddAsync(role);
        await db.PermissionGroups.AddAsync(group);
        await db.Permissions.AddAsync(menu);
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
            PermissionId = menu.EntityId
        };
        await db.RolePermissions.AddAsync(permission);

        await db.SaveChangesAsync();

        InMemoryTenantQuotaStore quotaStore = new();
        await quotaStore.SaveQuotaAsync(TenantContext.CurrentTenantId, TenantQuotaPresets.Free);
        PermissionService<TestPerm, TestDbContext> service = CreateService(db, new MAuthenticateInfoContext(false), quotaStore: quotaStore);
        MResponse<MUiEngineManifest> freeResponse = await service.GetUiEngineManifestAsync(user.EntityId, CancellationToken.None);
        MUiEngineManifest freeManifest = Assert.IsType<MUiEngineManifest>(freeResponse.Result);

        MUiEngineScreen freeScreen = Assert.Single(freeManifest.Screens, x => x.ScreenKey == "screen:rule-engine");
        MUiEngineScreen freeDecisionScreen = Assert.Single(freeManifest.Screens, x => x.ScreenKey == "screen:decision-table-list");
        Assert.Equal(TenantTier.Free.ToString(), freeManifest.LicenseTier);
        Assert.Equal("rule-engine", freeScreen.RequiredCapability);
        Assert.False(freeScreen.IsEnabled);
        Assert.Equal("RuleEngineUpgradePrompt", Assert.Single(freeScreen.Components).ComponentType);
        Assert.Equal("decision-tables", freeDecisionScreen.RequiredCapability);
        Assert.False(freeDecisionScreen.IsEnabled);
        Assert.Equal("DecisionTableUpgradePrompt", Assert.Single(freeDecisionScreen.Components).ComponentType);

        await quotaStore.SaveQuotaAsync(TenantContext.CurrentTenantId, TenantQuotaPresets.Starter);
        MResponse<MUiEngineManifest> starterResponse = await service.GetUiEngineManifestAsync(user.EntityId, CancellationToken.None);
        MUiEngineManifest starterManifest = Assert.IsType<MUiEngineManifest>(starterResponse.Result);
        MUiEngineScreen starterScreen = Assert.Single(starterManifest.Screens, x => x.ScreenKey == "screen:rule-engine");
        MUiEngineScreen starterDecisionScreen = Assert.Single(starterManifest.Screens, x => x.ScreenKey == "screen:decision-table-list");

        Assert.Equal(TenantTier.Starter.ToString(), starterManifest.LicenseTier);
        Assert.True(starterScreen.IsEnabled);
        Assert.Equal("page-content", Assert.Single(starterScreen.Components).ComponentType);
        Assert.False(starterDecisionScreen.IsEnabled);
        Assert.Equal("DecisionTableUpgradePrompt", Assert.Single(starterDecisionScreen.Components).ComponentType);
    }

    [Fact]
    public async Task GetUiEngineSchemaVersionAsync_Returns_Deterministic_Hash()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using TestDbContext db = new(options);
        PermissionService<TestPerm, TestDbContext> service = CreateService(db, new MAuthenticateInfoContext(false));

        MResponse<MUiEngineSchemaVersion> first = await service.GetUiEngineSchemaVersionAsync(CancellationToken.None);
        MResponse<MUiEngineSchemaVersion> second = await service.GetUiEngineSchemaVersionAsync(CancellationToken.None);

        Assert.NotNull(first.Result);
        Assert.NotNull(second.Result);
        Assert.Equal(first.Result!.SchemaHash, second.Result!.SchemaHash);
        Assert.Equal(MUiEngineManifest.MSchemaVersionV1, first.Result.Version);
        Assert.Equal(first.Result.SchemaHash, first.Result.OpenApiHash);
    }

    [Fact]
    public async Task NotifyUiEngineSchemaChangeAsync_Triggers_Notifier()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using TestDbContext db = new(options);
        IUiEngineSchemaNotifier notifier = Substitute.For<IUiEngineSchemaNotifier>();
        PermissionService<TestPerm, TestDbContext> service = CreateService(db, new MAuthenticateInfoContext(false), schemaNotifier: notifier);

        MUiEngineSchemaChangeNotification notification = new()
        {
            SchemaHash = "abc123",
            Source = "test"
        };
        MResponse<object> response = await service.NotifyUiEngineSchemaChangeAsync(
            notification,
            CancellationToken.None);

        Assert.True(response.IsOk);
        await notifier.Received(1).NotifySchemaChangedAsync(
            Arg.Is<MUiEngineSchemaVersion>(x => x.SchemaHash == "abc123" && x.OpenApiHash == "abc123"),
            Arg.Any<CancellationToken>());
    }
}
