namespace Muonroi.Core.Abstractions.Tests;

public class PermissionTreeTests
{
    [Fact]
    public void PermissionTree_DefaultMenus_IsEmpty()
    {
        PermissionTree tree = new();

        tree.Menus.Should().BeEmpty();
    }

    [Fact]
    public void MenuPermission_Properties_CanBeSetAndRetrieved()
    {
        MenuPermission menu = new()
        {
            Key = "dashboard",
            CanView = true,
            Actions = [new ActionPermission { Key = "create", CanExec = true }],
            Tabs = [new TabPermission { Key = "overview", CanView = true }],
            Fields = [new FieldPermission { Key = "name", CanView = true, CanEdit = false }]
        };

        menu.Key.Should().Be("dashboard");
        menu.CanView.Should().BeTrue();
        menu.Actions.Should().HaveCount(1);
        menu.Actions[0].Key.Should().Be("create");
        menu.Actions[0].CanExec.Should().BeTrue();
        menu.Tabs.Should().HaveCount(1);
        menu.Tabs[0].Key.Should().Be("overview");
        menu.Fields.Should().HaveCount(1);
        menu.Fields[0].Key.Should().Be("name");
        menu.Fields[0].CanView.Should().BeTrue();
        menu.Fields[0].CanEdit.Should().BeFalse();
    }

    [Fact]
    public void PermissionTree_WithMultipleMenus_MaintainsOrder()
    {
        PermissionTree tree = new()
        {
            Menus =
            [
                new MenuPermission { Key = "menu1" },
                new MenuPermission { Key = "menu2" },
                new MenuPermission { Key = "menu3" }
            ]
        };

        tree.Menus.Should().HaveCount(3);
        tree.Menus[0].Key.Should().Be("menu1");
        tree.Menus[2].Key.Should().Be("menu3");
    }
}

public class PermissionDefinitionTests
{
    [Fact]
    public void PermissionDefinition_DefaultValues_AreCorrect()
    {
        PermissionDefinition def = new();

        def.Name.Should().BeEmpty();
        def.UiKey.Should().BeEmpty();
        def.Type.Should().Be(PermissionType.Menu);
        def.DisplayNames.Should().BeEmpty();
        def.Order.Should().Be(0);
        def.AllowedProviders.Should().BeEmpty();
        def.Icon.Should().BeNull();
        def.Description.Should().BeNull();
        def.GroupName.Should().BeEmpty();
        def.GroupDisplayName.Should().BeEmpty();
        def.Permissions.Should().BeEmpty();
    }

    [Fact]
    public void PermissionDefinition_CanSetAllProperties()
    {
        PermissionDefinition def = new()
        {
            Name = "ManageUsers",
            UiKey = "users.manage",
            Type = PermissionType.Action,
            DisplayNames = new() { ["en"] = "Manage Users", ["vi"] = "Quan ly nguoi dung" },
            Order = 10,
            AllowedProviders = ["User", "Role"],
            Icon = "pi-users",
            Description = "Allows managing users",
            GroupName = "UserManagement",
            GroupDisplayName = "User Management"
        };

        def.Name.Should().Be("ManageUsers");
        def.DisplayNames.Should().HaveCount(2);
        def.AllowedProviders.Should().Contain("Role");
    }
}

public class MControllerExecutionContextTests
{
    [Fact]
    public void Properties_CanBeInitialized()
    {
        MControllerExecutionContext ctx = new()
        {
            UserId = Guid.NewGuid(),
            Username = "admin",
            TenantId = "t1",
            TenantTier = "Enterprise",
            Actor = "system",
            IsAuthenticated = true,
            Permissions = ["read", "write"]
        };

        ctx.Username.Should().Be("admin");
        ctx.TenantId.Should().Be("t1");
        ctx.IsAuthenticated.Should().BeTrue();
        ctx.Permissions.Should().HaveCount(2);
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        MControllerExecutionContext ctx = new();

        ctx.UserId.Should().BeNull();
        ctx.Username.Should().BeNull();
        ctx.TenantId.Should().BeNull();
        ctx.IsAuthenticated.Should().BeFalse();
        ctx.Permissions.Should().BeEmpty();
    }
}
