namespace Muonroi.Core.Abstractions.Models;

/// <summary>
/// Represents a tree of permissions.
/// </summary>
public class PermissionTree
{
    /// <summary>
    /// The menu permissions.
    /// </summary>
    public List<MenuPermission> Menus { get; set; } = [];
}

/// <summary>
/// Represents permissions for a menu.
/// </summary>
public class MenuPermission
{
    /// <summary>
    /// The key for the menu.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Whether the menu can be viewed.
    /// </summary>
    public bool CanView { get; set; }
    /// <summary>
    /// The action permissions for the menu.
    /// </summary>
    public List<ActionPermission> Actions { get; set; } = [];
    /// <summary>
    /// The tab permissions for the menu.
    /// </summary>
    public List<TabPermission> Tabs { get; set; } = [];
    /// <summary>
    /// The field permissions for the menu.
    /// </summary>
    public List<FieldPermission> Fields { get; set; } = [];
}

/// <summary>
/// Represents permissions for an action.
/// </summary>
public class ActionPermission
{
    /// <summary>
    /// The key for the action.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Whether the action can be executed.
    /// </summary>
    public bool CanExec { get; set; }
}

/// <summary>
/// Represents permissions for a tab.
/// </summary>
public class TabPermission
{
    /// <summary>
    /// The key for the tab.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Whether the tab can be viewed.
    /// </summary>
    public bool CanView { get; set; }
}

/// <summary>
/// Represents permissions for a field.
/// </summary>
public class FieldPermission
{
    /// <summary>
    /// The key for the field.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Whether the field can be viewed.
    /// </summary>
    public bool CanView { get; set; }
    /// <summary>
    /// Whether the field can be edited.
    /// </summary>
    public bool CanEdit { get; set; }
}
