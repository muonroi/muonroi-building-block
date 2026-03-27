using Microsoft.EntityFrameworkCore;
using Muonroi.AspNetCore.Services;
using Muonroi.AspNetCore.Tests.Helpers;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.Core.Abstractions.Models.Common;
using Muonroi.Core.Abstractions.Response;
using Muonroi.Data.EntityFrameworkCore.Entity.Identity;
using Muonroi.Tenancy.Core;
using NSubstitute;
using Xunit;

namespace Muonroi.AspNetCore.Tests.Permissions;

public class PermissionServiceUiManifestTests
{
    private static PermissionService<TestPerm, TestDbContext> CreateService(
        TestDbContext db,
        MAuthenticateInfoContext ctx,
        ILicenseGuard? guard = null)
    {
        TenantContext.CurrentTenantId ??= Guid.NewGuid().ToString();
        return new PermissionService<TestPerm, TestDbContext>(db, ctx, new FakeDateTimeService(), null, guard);
    }

    [Fact]
    public async Task GetUiManifestAsync_Composes_Visibility_And_Enablement_From_Backend()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using TestDbContext db = new(options);

        MUser user = new()
        {
            UserName = "manifest-user",
            EmailAddress = "manifest@local",
            Name = "Manifest",
            Surname = "User",
            Password = "p"
        };

        MRole role = new()
        {
            Name = "manifest-role",
            DisplayName = "Manifest role",
            NormalizedName = "MANIFEST-ROLE"
        };

        MPermissionGroup group = new()
        {
            Name = "Administration",
            DisplayName = "Administration"
        };

        MPermission menu = new()
        {
            Name = "Admin_Menu",
            UiKey = "admin",
            Type = PermissionType.Menu,
            Label = "Admin",
            IsGranted = true,
            PermissionGroup = group,
            Order = 1
        };

        MPermission createAction = new()
        {
            Name = "Admin_Roles_Create",
            UiKey = "admin.roles.create",
            Parent = menu,
            Type = PermissionType.Action,
            Label = "Create role",
            IsGranted = true,
            PermissionGroup = group,
            Order = 2
        };

        MPermission editAction = new()
        {
            Name = "Admin_Roles_Edit",
            UiKey = "admin.roles.edit",
            Parent = menu,
            Type = PermissionType.Action,
            Label = "Edit role",
            IsGranted = true,
            PermissionGroup = group,
            Order = 3
        };

        MPermission unpublishedAction = new()
        {
            Name = "Admin_Roles_Delete",
            UiKey = "admin.roles.delete",
            Parent = menu,
            Type = PermissionType.Action,
            Label = "Delete role",
            IsGranted = false,
            PermissionGroup = group,
            Order = 4
        };

        await db.Users.AddAsync(user);
        await db.Roles.AddAsync(role);
        await db.PermissionGroups.AddAsync(group);
        await db.Permissions.AddRangeAsync(menu, createAction, editAction, unpublishedAction);
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
            PermissionId = createAction.EntityId
        };
        await db.RolePermissions.AddAsync(permission);

        await db.SaveChangesAsync();

        PermissionService<TestPerm, TestDbContext> service = CreateService(db, new MAuthenticateInfoContext(false));

        MResponse<MUiManifest> response = await service.GetUiManifestAsync(user.EntityId, CancellationToken.None);

        MUiManifest manifest = Assert.IsType<MUiManifest>(response.Result);
        Assert.Equal(MUiManifest.MSchemaVersionV1, manifest.SchemaVersion);
        Assert.Equal(user.EntityId, manifest.UserId);
        Assert.Single(manifest.Groups);

        MUiManifestItem rootMenu = Assert.Single(manifest.Groups[0].Items);
        Assert.Equal("admin", rootMenu.UiKey);
        Assert.True(rootMenu.IsVisible);
        Assert.False(rootMenu.IsEnabled);
        Assert.Equal("permission_denied", rootMenu.DisabledReason);

        MUiManifestItem create = Assert.Single(rootMenu.Children, x => x.UiKey == "admin.roles.create");
        Assert.True(create.IsVisible);
        Assert.True(create.IsEnabled);
        Assert.Null(create.DisabledReason);
        Assert.Equal("/admin/roles/create", create.Route);

        MUiManifestItem edit = Assert.Single(rootMenu.Children, x => x.UiKey == "admin.roles.edit");
        Assert.False(edit.IsVisible);
        Assert.False(edit.IsEnabled);
        Assert.Equal("permission_denied", edit.DisabledReason);

        MUiManifestItem unpublished = Assert.Single(rootMenu.Children, x => x.UiKey == "admin.roles.delete");
        Assert.False(unpublished.IsVisible);
        Assert.False(unpublished.IsEnabled);
        Assert.Equal("permission_unpublished", unpublished.DisabledReason);
    }

    [Fact]
    public async Task GetUiManifestAsync_Uses_AdvancedAuth_License_Guard()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using TestDbContext db = new(options);
        ILicenseGuard guard = Substitute.For<ILicenseGuard>();
        PermissionService<TestPerm, TestDbContext> service = CreateService(db, new MAuthenticateInfoContext(false), guard);

        _ = await service.GetUiManifestAsync(Guid.NewGuid(), CancellationToken.None);

        guard.Received(1).EnsureFeature(FreeTierFeatures.Premium.AdvancedAuth);
    }

    [Theory]
    [InlineData("Admin_Roles_Create", "/admin/roles/create")]
    [InlineData("admin.roles.edit", "/admin/roles/edit")]
    [InlineData("", "/")]
    [InlineData(" ", "/")]
    public void MUiRouteBuilder_Normalizes_Route(string uiKey, string expectedRoute)
    {
        string route = MUiRouteBuilder.Build(uiKey);
        Assert.Equal(expectedRoute, route);
    }
}
