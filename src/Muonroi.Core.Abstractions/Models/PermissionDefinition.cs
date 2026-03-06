
namespace Muonroi.Core.Abstractions.Models;

/// <summary>
/// Metadata information for a single permission.
/// </summary>
public class PermissionDefinition
{
    /// <summary>
    /// Name of the permission (usually from enum).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// UI key that represents the permission on the frontend.
    /// </summary>
    public string UiKey { get; set; } = string.Empty;

    /// <summary>
    /// Permission type (Menu, Tab, Action, ...).
    /// </summary>
    public PermissionType Type { get; set; }

    /// <summary>
    /// Display names per language. Key is culture code (vi, en, ...).
    /// </summary>
    public Dictionary<string, string> DisplayNames { get; set; } = [];

    /// <summary>
    /// Order for UI rendering.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Allowed providers for assigning this permission (User, Role, Tenant, ...).
    /// </summary>
    public List<string> AllowedProviders { get; set; } = [];

    /// <summary>
    /// Icon to display on the UI.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Description for the permission.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Name of the group this permission belongs to.
    /// </summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the group.
    /// </summary>
    public string GroupDisplayName { get; set; } = string.Empty;
    public IEnumerable<string> Permissions { get; set; } = [];
}
