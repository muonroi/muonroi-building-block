namespace Muonroi.Core.Abstractions.Models;

/// <summary>
/// Represents a UI manifest.
/// </summary>
public sealed class MUiManifest
{
    /// <summary>
    /// Schema version V1.
    /// </summary>
    public const string MSchemaVersionV1 = "mui.manifest.v1";

    /// <summary>
    /// The schema version.
    /// </summary>
    public string SchemaVersion { get; set; } = MSchemaVersionV1;
    /// <summary>
    /// The generation date in UTC.
    /// </summary>
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
    /// <summary>
    /// The user ID.
    /// </summary>
    public Guid UserId { get; set; }
    /// <summary>
    /// The tenant ID.
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// The manifest groups.
    /// </summary>
    public List<MUiManifestGroup> Groups { get; set; } = [];
}

/// <summary>
/// Represents a group in a UI manifest.
/// </summary>
public sealed class MUiManifestGroup
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
    /// The items in this group.
    /// </summary>
    public List<MUiManifestItem> Items { get; set; } = [];
}

/// <summary>
/// Represents an item in a UI manifest.
/// </summary>
public sealed class MUiManifestItem
{
    /// <summary>
    /// The permission name.
    /// </summary>
    public string PermissionName { get; set; } = string.Empty;
    /// <summary>
    /// The UI key.
    /// </summary>
    public string UiKey { get; set; } = string.Empty;
    /// <summary>
    /// The parent UI key.
    /// </summary>
    public string? ParentUiKey { get; set; }
    /// <summary>
    /// The permission type.
    /// </summary>
    public PermissionType Type { get; set; }
    /// <summary>
    /// The display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>
    /// The icon.
    /// </summary>
    public string? Icon { get; set; }
    /// <summary>
    /// The description.
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// The display order.
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// The route.
    /// </summary>
    public string Route { get; set; } = "/";
    /// <summary>
    /// Whether the item is published.
    /// </summary>
    public bool IsPublished { get; set; }
    /// <summary>
    /// Whether the item is granted.
    /// </summary>
    public bool IsGranted { get; set; }
    /// <summary>
    /// Whether the item is visible.
    /// </summary>
    public bool IsVisible { get; set; }
    /// <summary>
    /// Whether the item is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
    /// <summary>
    /// The reason for disablement.
    /// </summary>
    public string? DisabledReason { get; set; }
    /// <summary>
    /// The children of this item.
    /// </summary>
    public List<MUiManifestItem> Children { get; set; } = [];

    /// <summary>
    /// Whether the item is hidden.
    /// </summary>
    public bool IsHidden => !IsVisible;
}

/// <summary>
/// Helper for building UI routes.
/// </summary>
public static class MUiRouteBuilder
{
    /// <summary>
    /// Builds a route from a UI key.
    /// </summary>
    /// <param name="uiKey">The UI key.</param>
    /// <returns>The built route.</returns>
    public static string Build(string? uiKey)
    {
        if (string.IsNullOrWhiteSpace(uiKey))
        {
            return "/";
        }

        string path = uiKey.Trim()
            .Replace("_", "/", StringComparison.Ordinal)
            .Replace(".", "/", StringComparison.Ordinal)
            .Replace(" ", "-", StringComparison.Ordinal)
            .ToLowerInvariant();

        while (path.Contains("//", StringComparison.Ordinal))
        {
            path = path.Replace("//", "/", StringComparison.Ordinal);
        }

        path = path.Trim('/');
        return path.Length == 0 ? "/" : "/" + path;
    }
}

/// <summary>
/// Represents a UI engine manifest.
/// </summary>
public sealed class MUiEngineManifest
{
    /// <summary>
    /// Schema version V1.
    /// </summary>
    public const string MSchemaVersionV1 = "mui.engine.v1";
    /// <summary>
    /// Schema version V2.
    /// </summary>
    public const string MSchemaVersionV2 = "mui.engine.v2";

    /// <summary>
    /// The schema version.
    /// </summary>
    public string SchemaVersion { get; set; } = MSchemaVersionV2;
    /// <summary>
    /// The generation date in UTC.
    /// </summary>
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
    /// <summary>
    /// The user ID.
    /// </summary>
    public Guid UserId { get; set; }
    /// <summary>
    /// The tenant ID.
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// The license tier.
    /// </summary>
    public string LicenseTier { get; set; } = "Free";
    /// <summary>
    /// The capabilities.
    /// </summary>
    public List<MUiEngineCapability> Capabilities { get; set; } = [];
    /// <summary>
    /// The navigation groups.
    /// </summary>
    public List<MUiEngineNavigationGroup> NavigationGroups { get; set; } = [];
    /// <summary>
    /// The screens.
    /// </summary>
    public List<MUiEngineScreen> Screens { get; set; } = [];
    /// <summary>
    /// The actions.
    /// </summary>
    public List<MUiEngineAction> Actions { get; set; } = [];
    /// <summary>
    /// The data sources.
    /// </summary>
    public List<MUiEngineDataSource> DataSources { get; set; } = [];
    /// <summary>
    /// The component registry.
    /// </summary>
    public MUiEngineComponentRegistry ComponentRegistry { get; set; } = new();
    /// <summary>
    /// The app shell.
    /// </summary>
    public MUiEngineAppShell? AppShell { get; set; }
    /// <summary>
    /// The authentication profile.
    /// </summary>
    public MUiEngineAuthProfile? AuthProfile { get; set; }
    /// <summary>
    /// The API contracts.
    /// </summary>
    public List<MUiEngineApiContract>? ApiContracts { get; set; }
    /// <summary>
    /// The rule bindings.
    /// </summary>
    public List<MUiEngineRuleBinding>? RuleBindings { get; set; }
    /// <summary>
    /// The generation hints.
    /// </summary>
    public MUiEngineGenerationHints? GenerationHints { get; set; }
}

/// <summary>
/// Represents the app shell for the UI engine.
/// </summary>
public sealed class MUiEngineAppShell
{
    /// <summary>
    /// The root layout.
    /// </summary>
    public string RootLayout { get; set; } = "default";
    /// <summary>
    /// The slots.
    /// </summary>
    public List<string> Slots { get; set; } = [];
    /// <summary>
    /// The theme.
    /// </summary>
    public string? Theme { get; set; }
    /// <summary>
    /// The logo URL.
    /// </summary>
    public string? LogoUrl { get; set; }
    /// <summary>
    /// The favicon URL.
    /// </summary>
    public string? FaviconUrl { get; set; }
}

/// <summary>
/// Represents the authentication profile for the UI engine.
/// </summary>
public sealed class MUiEngineAuthProfile
{
    /// <summary>
    /// The source of the token.
    /// </summary>
    public string TokenSource { get; set; } = "header";
    /// <summary>
    /// The key for the token.
    /// </summary>
    public string TokenKey { get; set; } = "Authorization";
    /// <summary>
    /// The path for refreshing the token.
    /// </summary>
    public string? RefreshPath { get; set; }
    /// <summary>
    /// The tenant header key.
    /// </summary>
    public string TenantHeaderKey { get; set; } = "X-Tenant-Id";
    /// <summary>
    /// The correlation ID key.
    /// </summary>
    public string CorrelationIdKey { get; set; } = "X-Correlation-Id";
    /// <summary>
    /// The failure policy.
    /// </summary>
    public string FailurePolicy { get; set; } = "401";
}

/// <summary>
/// Represents an API contract for the UI engine.
/// </summary>
public sealed class MUiEngineApiContract
{
    /// <summary>
    /// The operation ID.
    /// </summary>
    public string OperationId { get; set; } = string.Empty;
    /// <summary>
    /// The endpoint path.
    /// </summary>
    public string EndpointPath { get; set; } = "/";
    /// <summary>
    /// The HTTP method.
    /// </summary>
    public string HttpMethod { get; set; } = "GET";
    /// <summary>
    /// Reference to the request schema.
    /// </summary>
    public string? RequestSchemaRef { get; set; }
    /// <summary>
    /// Reference to the response schema.
    /// </summary>
    public string? ResponseSchemaRef { get; set; }
    /// <summary>
    /// The tags.
    /// </summary>
    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// Represents a rule binding for the UI engine.
/// </summary>
public sealed class MUiEngineRuleBinding
{
    /// <summary>
    /// The endpoint route.
    /// </summary>
    public string EndpointRoute { get; set; } = "/";
    /// <summary>
    /// The name of the workflow.
    /// </summary>
    public string? WorkflowName { get; set; }
    /// <summary>
    /// The context type.
    /// </summary>
    public string? ContextType { get; set; }
    /// <summary>
    /// The ordered rules.
    /// </summary>
    public List<string> OrderedRules { get; set; } = [];
}

/// <summary>
/// Represents generation hints for the UI engine.
/// </summary>
public sealed class MUiEngineGenerationHints
{
    /// <summary>
    /// The output base path.
    /// </summary>
    public string? OutputBasePath { get; set; }
    /// <summary>
    /// The core output path.
    /// </summary>
    public string? CoreOutputPath { get; set; }
    /// <summary>
    /// The API output path.
    /// </summary>
    public string? ApiOutputPath { get; set; }
    /// <summary>
    /// The models output path.
    /// </summary>
    public string? ModelsOutputPath { get; set; }
    /// <summary>
    /// The features output path.
    /// </summary>
    public string? FeaturesOutputPath { get; set; }
    /// <summary>
    /// Whether watch mode is enabled.
    /// </summary>
    public bool WatchEnabled { get; set; }
}

/// <summary>
/// Represents a capability for the UI engine.
/// </summary>
public sealed class MUiEngineCapability
{
    /// <summary>
    /// The capability key.
    /// </summary>
    public string CapabilityKey { get; set; } = string.Empty;
    /// <summary>
    /// The display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>
    /// Whether the capability is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
    /// <summary>
    /// The required license tier.
    /// </summary>
    public string RequiredTier { get; set; } = "Free";
    /// <summary>
    /// Component overrides.
    /// </summary>
    public Dictionary<string, string> ComponentOverrides { get; set; } = [];
    /// <summary>
    /// Action overrides.
    /// </summary>
    public Dictionary<string, string> ActionOverrides { get; set; } = [];
}

/// <summary>
/// Represents a navigation group for the UI engine.
/// </summary>
public sealed class MUiEngineNavigationGroup
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
    /// The navigation nodes in this group.
    /// </summary>
    public List<MUiEngineNavigationNode> Items { get; set; } = [];
}

/// <summary>
/// Represents a navigation node for the UI engine.
/// </summary>
public sealed class MUiEngineNavigationNode
{
    /// <summary>
    /// The node key.
    /// </summary>
    public string NodeKey { get; set; } = string.Empty;
    /// <summary>
    /// The UI key.
    /// </summary>
    public string UiKey { get; set; } = string.Empty;
    /// <summary>
    /// The parent UI key.
    /// </summary>
    public string? ParentUiKey { get; set; }
    /// <summary>
    /// The title.
    /// </summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>
    /// The route.
    /// </summary>
    public string Route { get; set; } = "/";
    /// <summary>
    /// The permission type.
    /// </summary>
    public PermissionType Type { get; set; }
    /// <summary>
    /// The icon.
    /// </summary>
    public string? Icon { get; set; }
    /// <summary>
    /// The display order.
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// Whether the node is visible.
    /// </summary>
    public bool IsVisible { get; set; }
    /// <summary>
    /// Whether the node is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
    /// <summary>
    /// The reason for disablement.
    /// </summary>
    public string? DisabledReason { get; set; }
    /// <summary>
    /// The key of the associated screen.
    /// </summary>
    public string? ScreenKey { get; set; }
    /// <summary>
    /// The keys of the associated actions.
    /// </summary>
    public List<string> ActionKeys { get; set; } = [];
    /// <summary>
    /// The children of this node.
    /// </summary>
    public List<MUiEngineNavigationNode> Children { get; set; } = [];
}

/// <summary>
/// Represents a screen for the UI engine.
/// </summary>
public sealed class MUiEngineScreen
{
    /// <summary>
    /// The screen key.
    /// </summary>
    public string ScreenKey { get; set; } = string.Empty;
    /// <summary>
    /// The UI key.
    /// </summary>
    public string UiKey { get; set; } = string.Empty;
    /// <summary>
    /// The title.
    /// </summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>
    /// The route.
    /// </summary>
    public string Route { get; set; } = "/";
    /// <summary>
    /// The required capability.
    /// </summary>
    public string? RequiredCapability { get; set; }
    /// <summary>
    /// Whether the screen is visible.
    /// </summary>
    public bool IsVisible { get; set; }
    /// <summary>
    /// Whether the screen is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
    /// <summary>
    /// The reason for disablement.
    /// </summary>
    public string? DisabledReason { get; set; }
    /// <summary>
    /// The key of the associated data source.
    /// </summary>
    public string? DataSourceKey { get; set; }
    /// <summary>
    /// The keys of the associated actions.
    /// </summary>
    public List<string> ActionKeys { get; set; } = [];
    /// <summary>
    /// The layout of the screen.
    /// </summary>
    public MUiEngineLayout Layout { get; set; } = new();
    /// <summary>
    /// The components on the screen.
    /// </summary>
    public List<MUiEngineComponent> Components { get; set; } = [];
}

/// <summary>
/// Represents a layout for the UI engine.
/// </summary>
public sealed class MUiEngineLayout
{
    /// <summary>
    /// The template name.
    /// </summary>
    public string Template { get; set; } = "default-page";
    /// <summary>
    /// The layout areas.
    /// </summary>
    public List<MUiEngineLayoutArea> Areas { get; set; } =
    [
        new()
        {
            AreaKey = "header",
            Purpose = "header",
            Order = 0
        },
        new()
        {
            AreaKey = "toolbar",
            Purpose = "toolbar",
            Order = 1
        },
        new()
        {
            AreaKey = "main",
            Purpose = "main",
            Order = 2
        }
    ];
}

/// <summary>
/// Represents a layout area for the UI engine.
/// </summary>
public sealed class MUiEngineLayoutArea
{
    /// <summary>
    /// The area key.
    /// </summary>
    public string AreaKey { get; set; } = string.Empty;
    /// <summary>
    /// The purpose of the area.
    /// </summary>
    public string Purpose { get; set; } = string.Empty;
    /// <summary>
    /// The display order.
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// Represents a component for the UI engine.
/// </summary>
public sealed class MUiEngineComponent
{
    /// <summary>
    /// The component key.
    /// </summary>
    public string ComponentKey { get; set; } = string.Empty;
    /// <summary>
    /// The UI key.
    /// </summary>
    public string UiKey { get; set; } = string.Empty;
    /// <summary>
    /// The key of the screen it belongs to.
    /// </summary>
    public string ScreenKey { get; set; } = string.Empty;
    /// <summary>
    /// The type of the component.
    /// </summary>
    public string ComponentType { get; set; } = "panel";
    /// <summary>
    /// The required capability.
    /// </summary>
    public string? RequiredCapability { get; set; }
    /// <summary>
    /// The slot it occupies.
    /// </summary>
    public string Slot { get; set; } = "main";
    /// <summary>
    /// The display order.
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// The key of the associated data source.
    /// </summary>
    public string? DataSourceKey { get; set; }
    /// <summary>
    /// The keys of the associated actions.
    /// </summary>
    public List<string> ActionKeys { get; set; } = [];
    /// <summary>
    /// The component properties.
    /// </summary>
    public Dictionary<string, string> Props { get; set; } = [];
}

/// <summary>
/// Represents an action for the UI engine.
/// </summary>
public sealed class MUiEngineAction
{
    /// <summary>
    /// The action key.
    /// </summary>
    public string ActionKey { get; set; } = string.Empty;
    /// <summary>
    /// The UI key.
    /// </summary>
    public string UiKey { get; set; } = string.Empty;
    /// <summary>
    /// The associated permission name.
    /// </summary>
    public string PermissionName { get; set; } = string.Empty;
    /// <summary>
    /// The label.
    /// </summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>
    /// The route.
    /// </summary>
    public string Route { get; set; } = "/";
    /// <summary>
    /// The type of action.
    /// </summary>
    public string ActionType { get; set; } = "command";
    /// <summary>
    /// The required capability.
    /// </summary>
    public string? RequiredCapability { get; set; }
    /// <summary>
    /// Whether the action is visible.
    /// </summary>
    public bool IsVisible { get; set; }
    /// <summary>
    /// Whether the action is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
    /// <summary>
    /// The reason for disablement.
    /// </summary>
    public string? DisabledReason { get; set; }
    /// <summary>
    /// The key of the target screen.
    /// </summary>
    public string? TargetScreenKey { get; set; }
}

/// <summary>
/// Represents a data source for the UI engine.
/// </summary>
public sealed class MUiEngineDataSource
{
    /// <summary>
    /// The data source key.
    /// </summary>
    public string DataSourceKey { get; set; } = string.Empty;
    /// <summary>
    /// The UI key.
    /// </summary>
    public string UiKey { get; set; } = string.Empty;
    /// <summary>
    /// The key of the associated screen.
    /// </summary>
    public string ScreenKey { get; set; } = string.Empty;
    /// <summary>
    /// The endpoint path.
    /// </summary>
    public string EndpointPath { get; set; } = "/";
    /// <summary>
    /// The HTTP method.
    /// </summary>
    public string HttpMethod { get; set; } = "GET";
    /// <summary>
    /// The request model name.
    /// </summary>
    public string? RequestModel { get; set; }
    /// <summary>
    /// The response model name.
    /// </summary>
    public string? ResponseModel { get; set; }
}

/// <summary>
/// Information about the UI engine contract.
/// </summary>
public sealed class MUiEngineContractInfo
{
    /// <summary>
    /// The runtime schema version.
    /// </summary>
    public string RuntimeSchemaVersion { get; set; } = MUiEngineManifest.MSchemaVersionV2;
    /// <summary>
    /// The supported schema versions.
    /// </summary>
    public List<string> SupportedSchemaVersions { get; set; } =
        [MUiEngineManifest.MSchemaVersionV1, MUiEngineManifest.MSchemaVersionV2];
    /// <summary>
    /// The current manifest endpoint.
    /// </summary>
    public string CurrentManifestEndpoint { get; set; } = "/api/v1/auth/ui-engine/current";
    /// <summary>
    /// The template for the user manifest endpoint.
    /// </summary>
    public string UserManifestEndpointTemplate { get; set; } = "/api/v1/auth/ui-engine/{userId}";
    /// <summary>
    /// The schema hash endpoint.
    /// </summary>
    public string SchemaHashEndpoint { get; set; } = "/api/v1/auth/ui-engine/schema-hash";
    /// <summary>
    /// The endpoint for change notification.
    /// </summary>
    public string NotifyChangeEndpoint { get; set; } = "/api/v1/auth/ui-engine/notify-change";
    /// <summary>
    /// The real-time hub endpoint.
    /// </summary>
    public string RealtimeHubEndpoint { get; set; } = "/hubs/ui-engine";
    /// <summary>
    /// The generation date in UTC.
    /// </summary>
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
}

/// <summary>
/// Represents a schema version for the UI engine.
/// </summary>
public sealed class MUiEngineSchemaVersion
{
    /// <summary>
    /// The version string.
    /// </summary>
    public string Version { get; set; } = MUiEngineManifest.MSchemaVersionV2;
    /// <summary>
    /// The schema hash.
    /// </summary>
    public string SchemaHash { get; set; } = string.Empty;
    /// <summary>
    /// The OpenAPI hash.
    /// </summary>
    public string OpenApiHash { get; set; } = string.Empty;
    /// <summary>
    /// The generation date in UTC.
    /// </summary>
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
}

/// <summary>
/// Represents a schema change notification.
/// </summary>
public sealed class MUiEngineSchemaChangeNotification
{
    /// <summary>
    /// The schema hash.
    /// </summary>
    public string? SchemaHash { get; set; }
    /// <summary>
    /// The source of the change.
    /// </summary>
    public string? Source { get; set; }
    /// <summary>
    /// The change date in UTC.
    /// </summary>
    public DateTime? ChangedAtUtc { get; set; }
}

/// <summary>
/// Helper for building UI engine keys.
/// </summary>
public static class MUiEngineKeyBuilder
{
    /// <summary>
    /// Builds a node key.
    /// </summary>
    /// <param name="uiKey">The UI key.</param>
    /// <returns>The built node key.</returns>
    public static string BuildNodeKey(string uiKey)
    {
        return $"node:{Normalize(uiKey)}";
    }

    /// <summary>
    /// Builds a screen key.
    /// </summary>
    /// <param name="uiKey">The UI key.</param>
    /// <returns>The built screen key.</returns>
    public static string BuildScreenKey(string uiKey)
    {
        return $"screen:{Normalize(uiKey)}";
    }

    /// <summary>
    /// Builds an action key.
    /// </summary>
    /// <param name="uiKey">The UI key.</param>
    /// <returns>The built action key.</returns>
    public static string BuildActionKey(string uiKey)
    {
        return $"action:{Normalize(uiKey)}";
    }

    /// <summary>
    /// Builds a data source key.
    /// </summary>
    /// <param name="uiKey">The UI key.</param>
    /// <returns>The built data source key.</returns>
    public static string BuildDataSourceKey(string uiKey)
    {
        return $"datasource:{Normalize(uiKey)}";
    }

    /// <summary>
    /// Builds a component key.
    /// </summary>
    /// <param name="uiKey">The UI key.</param>
    /// <param name="slot">The slot name.</param>
    /// <returns>The built component key.</returns>
    public static string BuildComponentKey(string uiKey, string slot)
    {
        return $"component:{Normalize(uiKey)}:{Normalize(slot)}";
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "root";
        }

        string normalized = value.Trim()
            .Replace("_", "-", StringComparison.Ordinal)
            .Replace(".", "-", StringComparison.Ordinal)
            .Replace(" ", "-", StringComparison.Ordinal)
            .ToLowerInvariant();

        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized.Trim('-');
    }
}
