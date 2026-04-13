namespace Muonroi.AspNetCore.Tests.Services;

public class UiManifestBuilderTests
{
    private static MPermission CreatePermission(
        Guid id,
        string name,
        string uiKey,
        PermissionType type = PermissionType.Menu,
        Guid? parentId = null,
        string? parentUiKey = null,
        bool isGranted = true,
        int order = 0,
        string? groupName = null,
        string? groupDisplayName = null)
    {
        return new MPermission
        {
            EntityId = id,
            Name = name,
            UiKey = uiKey,
            Type = type,
            ParentId = parentId,
            ParentUiKey = parentUiKey,
            IsGranted = isGranted,
            Order = order,
            Label = name,
            PermissionGroup = groupName != null ? new MPermissionGroup { Name = groupName, DisplayName = groupDisplayName ?? groupName } : null
        };
    }

    [Fact]
    public void BuildUiManifest_EmptyPermissions_ReturnsEmptyManifest()
    {
        var userId = Guid.NewGuid();
        var result = UiManifestBuilder.BuildUiManifest(
            userId, "tenant-1", [], new HashSet<Guid>());

        result.UserId.Should().Be(userId);
        result.TenantId.Should().Be("tenant-1");
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public void BuildUiManifest_SinglePermission_CreatesGroup()
    {
        var userId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var permissions = new List<MPermission>
        {
            CreatePermission(permId, "Dashboard", "dashboard", groupName: "main", groupDisplayName: "Main")
        };

        var result = UiManifestBuilder.BuildUiManifest(
            userId, "tenant-1", permissions, new HashSet<Guid> { permId });

        result.Groups.Should().HaveCount(1);
        result.Groups[0].GroupName.Should().Be("main");
        result.Groups[0].Items.Should().HaveCount(1);
        result.Groups[0].Items[0].UiKey.Should().Be("dashboard");
    }

    [Fact]
    public void BuildUiManifest_GrantedPermission_IsVisibleAndEnabled()
    {
        var permId = Guid.NewGuid();
        var permissions = new List<MPermission>
        {
            CreatePermission(permId, "Page", "page", isGranted: true)
        };

        var result = UiManifestBuilder.BuildUiManifest(
            Guid.NewGuid(), null, permissions, new HashSet<Guid> { permId });

        var item = result.Groups[0].Items[0];
        item.IsVisible.Should().BeTrue();
        item.IsEnabled.Should().BeTrue();
        item.DisabledReason.Should().BeNull();
    }

    [Fact]
    public void BuildUiManifest_UngrantedPermission_IsNotVisible()
    {
        var permId = Guid.NewGuid();
        var permissions = new List<MPermission>
        {
            CreatePermission(permId, "Page", "page", isGranted: true)
        };

        var result = UiManifestBuilder.BuildUiManifest(
            Guid.NewGuid(), null, permissions, new HashSet<Guid>());

        var item = result.Groups[0].Items[0];
        item.IsVisible.Should().BeFalse();
        item.IsEnabled.Should().BeFalse();
        item.DisabledReason.Should().Be("permission_denied");
    }

    [Fact]
    public void BuildUiManifest_UnpublishedPermission_ShowsUnpublishedReason()
    {
        var permId = Guid.NewGuid();
        var permissions = new List<MPermission>
        {
            CreatePermission(permId, "Page", "page", isGranted: false)
        };

        var result = UiManifestBuilder.BuildUiManifest(
            Guid.NewGuid(), null, permissions, new HashSet<Guid> { permId });

        var item = result.Groups[0].Items[0];
        item.DisabledReason.Should().Be("permission_unpublished");
    }

    [Fact]
    public void BuildUiManifest_ParentChild_BuildsHierarchy()
    {
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var permissions = new List<MPermission>
        {
            CreatePermission(parentId, "Settings", "settings", type: PermissionType.Menu),
            CreatePermission(childId, "General", "general", type: PermissionType.Tab,
                parentId: parentId, parentUiKey: "settings")
        };

        var grantedIds = new HashSet<Guid> { parentId, childId };
        var result = UiManifestBuilder.BuildUiManifest(Guid.NewGuid(), null, permissions, grantedIds);

        var parent = result.Groups[0].Items[0];
        parent.Children.Should().HaveCount(1);
        parent.Children[0].UiKey.Should().Be("general");
    }

    [Fact]
    public void BuildUiManifest_ContainerWithVisibleChild_IsVisible()
    {
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var permissions = new List<MPermission>
        {
            CreatePermission(parentId, "Menu", "menu", type: PermissionType.Menu, isGranted: true),
            CreatePermission(childId, "SubPage", "subpage", type: PermissionType.Tab,
                parentId: parentId, parentUiKey: "menu", isGranted: true)
        };

        var grantedIds = new HashSet<Guid> { childId };
        var result = UiManifestBuilder.BuildUiManifest(Guid.NewGuid(), null, permissions, grantedIds);

        var menu = result.Groups[0].Items[0];
        // The menu parent should become visible because it has a visible child
        menu.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void BuildUiManifest_ChildrenSortedByOrderThenUiKey()
    {
        var parentId = Guid.NewGuid();
        var child1Id = Guid.NewGuid();
        var child2Id = Guid.NewGuid();
        var permissions = new List<MPermission>
        {
            CreatePermission(parentId, "Menu", "menu", type: PermissionType.Menu),
            CreatePermission(child1Id, "Zebra", "zebra", type: PermissionType.Tab,
                parentId: parentId, parentUiKey: "menu", order: 2),
            CreatePermission(child2Id, "Alpha", "alpha", type: PermissionType.Tab,
                parentId: parentId, parentUiKey: "menu", order: 1)
        };

        var grantedIds = new HashSet<Guid> { parentId, child1Id, child2Id };
        var result = UiManifestBuilder.BuildUiManifest(Guid.NewGuid(), null, permissions, grantedIds);

        var children = result.Groups[0].Items[0].Children;
        children[0].UiKey.Should().Be("alpha"); // order 1
        children[1].UiKey.Should().Be("zebra"); // order 2
    }
}
