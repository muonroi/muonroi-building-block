namespace Muonroi.Core.Abstractions.Models;

public sealed class MUiManifest
{
    public const string MSchemaVersionV1 = "mui.manifest.v1";

    public string SchemaVersion { get; set; } = MSchemaVersionV1;
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
    public Guid UserId { get; set; }
    public string? TenantId { get; set; }
    public List<MUiManifestGroup> Groups { get; set; } = [];
}

public sealed class MUiManifestGroup
{
    public string GroupName { get; set; } = string.Empty;
    public string GroupDisplayName { get; set; } = string.Empty;
    public List<MUiManifestItem> Items { get; set; } = [];
}

public sealed class MUiManifestItem
{
    public string PermissionName { get; set; } = string.Empty;
    public string UiKey { get; set; } = string.Empty;
    public string? ParentUiKey { get; set; }
    public PermissionType Type { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public string Route { get; set; } = "/";
    public bool IsPublished { get; set; }
    public bool IsGranted { get; set; }
    public bool IsVisible { get; set; }
    public bool IsEnabled { get; set; }
    public string? DisabledReason { get; set; }
    public List<MUiManifestItem> Children { get; set; } = [];

    public bool IsHidden => !IsVisible;
}

public static class MUiRouteBuilder
{
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

public sealed class MUiEngineManifest
{
    public const string MSchemaVersionV1 = "mui.engine.v1";
    public const string MSchemaVersionV2 = "mui.engine.v2";

    public string SchemaVersion { get; set; } = MSchemaVersionV2;
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
    public Guid UserId { get; set; }
    public string? TenantId { get; set; }
    public string LicenseTier { get; set; } = "Free";
    public List<MUiEngineCapability> Capabilities { get; set; } = [];
    public List<MUiEngineNavigationGroup> NavigationGroups { get; set; } = [];
    public List<MUiEngineScreen> Screens { get; set; } = [];
    public List<MUiEngineAction> Actions { get; set; } = [];
    public List<MUiEngineDataSource> DataSources { get; set; } = [];
    public MUiEngineComponentRegistry ComponentRegistry { get; set; } = new();
    public MUiEngineAppShell? AppShell { get; set; }
    public MUiEngineAuthProfile? AuthProfile { get; set; }
    public List<MUiEngineApiContract>? ApiContracts { get; set; }
    public List<MUiEngineRuleBinding>? RuleBindings { get; set; }
    public MUiEngineGenerationHints? GenerationHints { get; set; }
}

public sealed class MUiEngineAppShell
{
    public string RootLayout { get; set; } = "default";
    public List<string> Slots { get; set; } = [];
    public string? Theme { get; set; }
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
}

public sealed class MUiEngineAuthProfile
{
    public string TokenSource { get; set; } = "header";
    public string TokenKey { get; set; } = "Authorization";
    public string? RefreshPath { get; set; }
    public string TenantHeaderKey { get; set; } = "X-Tenant-Id";
    public string CorrelationIdKey { get; set; } = "X-Correlation-Id";
    public string FailurePolicy { get; set; } = "401";
}

public sealed class MUiEngineApiContract
{
    public string OperationId { get; set; } = string.Empty;
    public string EndpointPath { get; set; } = "/";
    public string HttpMethod { get; set; } = "GET";
    public string? RequestSchemaRef { get; set; }
    public string? ResponseSchemaRef { get; set; }
    public List<string> Tags { get; set; } = [];
}

public sealed class MUiEngineRuleBinding
{
    public string EndpointRoute { get; set; } = "/";
    public string? WorkflowName { get; set; }
    public string? ContextType { get; set; }
    public List<string> OrderedRules { get; set; } = [];
}

public sealed class MUiEngineGenerationHints
{
    public string? OutputBasePath { get; set; }
    public string? CoreOutputPath { get; set; }
    public string? ApiOutputPath { get; set; }
    public string? ModelsOutputPath { get; set; }
    public string? FeaturesOutputPath { get; set; }
    public bool WatchEnabled { get; set; }
}

public sealed class MUiEngineCapability
{
    public string CapabilityKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string RequiredTier { get; set; } = "Free";
    public Dictionary<string, string> ComponentOverrides { get; set; } = [];
    public Dictionary<string, string> ActionOverrides { get; set; } = [];
}

public sealed class MUiEngineNavigationGroup
{
    public string GroupName { get; set; } = string.Empty;
    public string GroupDisplayName { get; set; } = string.Empty;
    public List<MUiEngineNavigationNode> Items { get; set; } = [];
}

public sealed class MUiEngineNavigationNode
{
    public string NodeKey { get; set; } = string.Empty;
    public string UiKey { get; set; } = string.Empty;
    public string? ParentUiKey { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Route { get; set; } = "/";
    public PermissionType Type { get; set; }
    public string? Icon { get; set; }
    public int Order { get; set; }
    public bool IsVisible { get; set; }
    public bool IsEnabled { get; set; }
    public string? DisabledReason { get; set; }
    public string? ScreenKey { get; set; }
    public List<string> ActionKeys { get; set; } = [];
    public List<MUiEngineNavigationNode> Children { get; set; } = [];
}

public sealed class MUiEngineScreen
{
    public string ScreenKey { get; set; } = string.Empty;
    public string UiKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Route { get; set; } = "/";
    public string? RequiredCapability { get; set; }
    public bool IsVisible { get; set; }
    public bool IsEnabled { get; set; }
    public string? DisabledReason { get; set; }
    public string? DataSourceKey { get; set; }
    public List<string> ActionKeys { get; set; } = [];
    public MUiEngineLayout Layout { get; set; } = new();
    public List<MUiEngineComponent> Components { get; set; } = [];
}

public sealed class MUiEngineLayout
{
    public string Template { get; set; } = "default-page";
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

public sealed class MUiEngineLayoutArea
{
    public string AreaKey { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public int Order { get; set; }
}

public sealed class MUiEngineComponent
{
    public string ComponentKey { get; set; } = string.Empty;
    public string UiKey { get; set; } = string.Empty;
    public string ScreenKey { get; set; } = string.Empty;
    public string ComponentType { get; set; } = "panel";
    public string? RequiredCapability { get; set; }
    public string Slot { get; set; } = "main";
    public int Order { get; set; }
    public string? DataSourceKey { get; set; }
    public List<string> ActionKeys { get; set; } = [];
    public Dictionary<string, string> Props { get; set; } = [];
}

public sealed class MUiEngineAction
{
    public string ActionKey { get; set; } = string.Empty;
    public string UiKey { get; set; } = string.Empty;
    public string PermissionName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Route { get; set; } = "/";
    public string ActionType { get; set; } = "command";
    public string? RequiredCapability { get; set; }
    public bool IsVisible { get; set; }
    public bool IsEnabled { get; set; }
    public string? DisabledReason { get; set; }
    public string? TargetScreenKey { get; set; }
}

public sealed class MUiEngineDataSource
{
    public string DataSourceKey { get; set; } = string.Empty;
    public string UiKey { get; set; } = string.Empty;
    public string ScreenKey { get; set; } = string.Empty;
    public string EndpointPath { get; set; } = "/";
    public string HttpMethod { get; set; } = "GET";
    public string? RequestModel { get; set; }
    public string? ResponseModel { get; set; }
}

public sealed class MUiEngineContractInfo
{
    public string RuntimeSchemaVersion { get; set; } = MUiEngineManifest.MSchemaVersionV2;
    public List<string> SupportedSchemaVersions { get; set; } =
        [MUiEngineManifest.MSchemaVersionV1, MUiEngineManifest.MSchemaVersionV2];
    public string CurrentManifestEndpoint { get; set; } = "/api/v1/auth/ui-engine/current";
    public string UserManifestEndpointTemplate { get; set; } = "/api/v1/auth/ui-engine/{userId}";
    public string SchemaHashEndpoint { get; set; } = "/api/v1/auth/ui-engine/schema-hash";
    public string NotifyChangeEndpoint { get; set; } = "/api/v1/auth/ui-engine/notify-change";
    public string RealtimeHubEndpoint { get; set; } = "/hubs/ui-engine";
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
}

public sealed class MUiEngineSchemaVersion
{
    public string Version { get; set; } = MUiEngineManifest.MSchemaVersionV2;
    public string SchemaHash { get; set; } = string.Empty;
    public string OpenApiHash { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
}

public sealed class MUiEngineSchemaChangeNotification
{
    public string? SchemaHash { get; set; }
    public string? Source { get; set; }
    public DateTime? ChangedAtUtc { get; set; }
}

public static class MUiEngineKeyBuilder
{
    public static string BuildNodeKey(string uiKey)
    {
        return $"node:{Normalize(uiKey)}";
    }

    public static string BuildScreenKey(string uiKey)
    {
        return $"screen:{Normalize(uiKey)}";
    }

    public static string BuildActionKey(string uiKey)
    {
        return $"action:{Normalize(uiKey)}";
    }

    public static string BuildDataSourceKey(string uiKey)
    {
        return $"datasource:{Normalize(uiKey)}";
    }

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
