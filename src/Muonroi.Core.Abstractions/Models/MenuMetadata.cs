namespace Muonroi.Core.Abstractions.Models;

public class ActionMetadata
{
    public string Name { get; set; } = string.Empty;
    public string UiKey { get; set; } = string.Empty;
    public PermissionType Type { get; set; }
    public bool IsGranted { get; set; }
    public List<ActionMetadata> Children { get; set; } = [];
}

public class MenuMetadata
{
    public string GroupName { get; set; } = string.Empty;
    public string GroupDisplayName { get; set; } = string.Empty;
    public List<ActionMetadata> Actions { get; set; } = [];
}
