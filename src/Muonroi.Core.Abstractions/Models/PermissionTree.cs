namespace Muonroi.Core.Abstractions.Models;

public class PermissionTree
{
    public List<MenuPermission> Menus { get; set; } = [];
}

public class MenuPermission
{
    public string Key { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public List<ActionPermission> Actions { get; set; } = [];
    public List<TabPermission> Tabs { get; set; } = [];
    public List<FieldPermission> Fields { get; set; } = [];
}

public class ActionPermission
{
    public string Key { get; set; } = string.Empty;
    public bool CanExec { get; set; }
}

public class TabPermission
{
    public string Key { get; set; } = string.Empty;
    public bool CanView { get; set; }
}

public class FieldPermission
{
    public string Key { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
}
