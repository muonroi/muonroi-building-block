namespace Muonroi.Core.Abstractions.Models;

/// <summary>
/// Metadata for an action.
/// </summary>
public class ActionMetadata
{
    /// <summary>
    /// The name of the action.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// The UI key.
    /// </summary>
    public string UiKey { get; set; } = string.Empty;
    /// <summary>
    /// The permission type.
    /// </summary>
    public PermissionType Type { get; set; }
    /// <summary>
    /// Whether the action is granted.
    /// </summary>
    public bool IsGranted { get; set; }
    /// <summary>
    /// The children of this action.
    /// </summary>
    public List<ActionMetadata> Children { get; set; } = [];
}

/// <summary>
/// Metadata for a menu.
/// </summary>
public class MenuMetadata
{
    /// <summary>
    /// The group name.
    /// </summary>
    public string GroupName { get; set; } = string.Empty;
    /// <summary>
    /// The group display name.
    /// </summary>
    public string GroupDisplayName { get; set; } = string.Empty;
    /// <summary>
    /// The actions in this menu.
    /// </summary>
    public List<ActionMetadata> Actions { get; set; } = [];
}
