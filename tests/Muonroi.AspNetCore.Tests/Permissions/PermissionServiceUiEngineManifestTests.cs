using Microsoft.EntityFrameworkCore;
using Muonroi.AspNetCore.Services;
using Muonroi.AspNetCore.Tests.Helpers;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.Core.Abstractions.Models.Common;
using Muonroi.Core.Abstractions.Response;
using Muonroi.Data.EntityFrameworkCore.Entity.Identity;
using Muonroi.Governance.Authorization;
using Muonroi.Quota.Abstractions;
using Muonroi.Tenancy.Abstractions.Interfaces;
using Muonroi.Tenancy.Core;
using Muonroi.Tenancy.Core.Shared;
using NSubstitute;
using Xunit;

namespace Muonroi.AspNetCore.Tests.Permissions;

public class PermissionServiceUiEngineManifestTests
{
    private sealed class FakeDecisionTableContributor : IUiEngineManifestContributor
    {
        public int Order => 100;
        public string ModuleId => "decision-table";
        public string RequiredTier => "Professional";

        public Task ContributeAsync(UiEngineManifestContext context, CancellationToken ct = default)
        {
            string listUiKey = "decision.table.list";
            string listScreenKey = MUiEngineKeyBuilder.BuildScreenKey(listUiKey);
            string listDataSourceKey = MUiEngineKeyBuilder.BuildDataSourceKey(listUiKey);

            if (context.Manifest.Screens.All(x => x.ScreenKey != listScreenKey))
            {
                context.Manifest.Screens.Add(new MUiEngineScreen
                {
                    ScreenKey = listScreenKey,
                    UiKey = listUiKey,
                    Title = "Decision Tables",
                    Route = "/decision-tables",
                    RequiredCapability = "decision-table",
                    IsVisible = true,
                    IsEnabled = true,
                    DataSourceKey = listDataSourceKey,
                    Components =
                    [
                        new MUiEngineComponent
                        {
                            ComponentKey = MUiEngineKeyBuilder.BuildComponentKey(listUiKey, "main"),
                            UiKey = listUiKey,
                            ScreenKey = listScreenKey,
                            ComponentType = "decision-table-list",
                            RequiredCapability = "decision-table",
                            Slot = "main",
                            Order = 0,
                            DataSourceKey = listDataSourceKey
                        }
                    ]
                });
            }

            var cap = context.Manifest.Capabilities.FirstOrDefault(x => x.CapabilityKey == "decision-table");
            if (cap != null)
            {
                cap.ComponentOverrides["decision-table-list"] = "DecisionTableUpgradePrompt";
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeRuleEngineContributor : IUiEngineManifestContributor
    {
        public int Order => 140;
        public string ModuleId => "rule-engine";
        public string RequiredTier => "Starter";

        public Task ContributeAsync(UiEngineManifestContext context, CancellationToken ct = default)
        {
            string uiKey = "rule.engine";
            string screenKey = MUiEngineKeyBuilder.BuildScreenKey(uiKey);
            string dataSourceKey = MUiEngineKeyBuilder.BuildDataSourceKey(uiKey);

            if (context.Manifest.Screens.All(x => x.ScreenKey != screenKey))
            {
                context.Manifest.Screens.Add(new MUiEngineScreen
                {
                    ScreenKey = screenKey,
                    UiKey = uiKey,
                    Title = "Rule Engine",
                    Route = "/rule-engine",
                    RequiredCapability = "rule-engine",
                    IsVisible = true,
                    IsEnabled = true,
                    DataSourceKey = dataSourceKey,
                    Components =
                    [
                        new MUiEngineComponent
                        {
                            ComponentKey = MUiEngineKeyBuilder.BuildComponentKey(uiKey, "main"),
                            UiKey = uiKey,
                            ScreenKey = screenKey,
                            ComponentType = "page-content",
                            RequiredCapability = "rule-engine",
                            Slot = "main",
                            Order = 0,
                            DataSourceKey = dataSourceKey
                        }
                    ]
                });
            }

            var cap = context.Manifest.Capabilities.FirstOrDefault(x => x.CapabilityKey == "rule-engine");
            if (cap != null)
            {
                cap.ComponentOverrides["page-content"] = "RuleEngineUpgradePrompt";
            }

            return Task.CompletedTask;
        }
    }

    private static PermissionService<TestPerm, TestDbContext> CreateService(
        TestDbContext db,
        MAuthenticateInfoContext ctx,
        ILicenseGuard? guard = null,
        ITenantQuotaStore? quotaStore = null,
        IUiEngineSchemaNotifier? schemaNotifier = null)
    {
        TenantContext.CurrentTenantId ??= Guid.NewGuid().ToString();
        List<IUiEngineManifestContributor> contributors =
        [
            new FakeDecisionTableContributor(),
            new FakeRuleEngineContributor()
        ];
        return new PermissionService<TestPerm, TestDbContext>(db, ctx, new FakeDateTimeService(), null, guard, quotaStore, schemaNotifier, contributors);
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
        // Schema version in source was V1, but model default is V2 now.
        Assert.Equal(MUiEngineManifest.MSchemaVersionV2, manifest.SchemaVersion);
        Assert.Equal(user.EntityId, manifest.UserId);
        Assert.Equal(TenantTier.Free.ToString(), manifest.LicenseTier);
        
        // Count assertions depend on logic in service. 
        Assert.NotEmpty(manifest.Capabilities);
        Assert.NotEmpty(manifest.NavigationGroups);
        Assert.NotEmpty(manifest.Screens);
        Assert.NotEmpty(manifest.Actions);
        Assert.NotEmpty(manifest.DataSources);

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
        Assert.Equal("decision-table", decisionScreen.RequiredCapability);
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

        Assert.Equal(MUiEngineManifest.MSchemaVersionV2, info.RuntimeSchemaVersion);
        Assert.Contains(MUiEngineManifest.MSchemaVersionV2, info.SupportedSchemaVersions);
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

        Quota.Abstractions.InMemoryTenantQuotaStore quotaStore = new(new FakeDateTimeService(), new FakeJsonSerializeService());
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
        Assert.Equal("decision-table", freeDecisionScreen.RequiredCapability);
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
        Assert.Equal(MUiEngineManifest.MSchemaVersionV2, first.Result.Version);
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
