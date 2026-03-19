using Muonroi.UiEngine.Catalog.Models;
using Muonroi.UiEngine.Catalog.Services;

namespace Muonroi.AspNetCore.Services;

/// <summary>
/// Orchestrates the building of the UI engine manifest by coordinating various contributors and services.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
public sealed class UiEngineManifestOrchestrator<TDbContext>(
    PermissionQueryService<TDbContext> permissionQueryService,
    UiEngineCapabilityResolver capabilityResolver,
    IEnumerable<IUiEngineManifestContributor> contributors,
    IServiceProvider? services = null,
    ITenantQuotaStore? tenantQuotaStore = null)
    where TDbContext : MDbContext
{
    private readonly IReadOnlyList<IUiEngineManifestContributor> _contributors = [.. contributors.OrderBy(x => x.Order)];

    /// <summary>
    /// Builds the UI engine manifest for the specified user asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{MUiEngineManifest}"/> representing the built engine manifest.</returns>
    public async Task<MUiEngineManifest> BuildAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        List<Guid> grantedIds = await permissionQueryService.GetGrantedPermissionIdsAsync(userId, cancellationToken);
        List<MPermission> permissions = await permissionQueryService.GetAllPermissionsWithGroupsAsync(cancellationToken);
        List<string> userPermissions = await permissionQueryService.GetPermissionNamesOfUserAsync(userId, cancellationToken);

        MUiManifest manifest = UiManifestBuilder.BuildUiManifest(
            userId,
            TenantContext.CurrentTenantId,
            permissions,
            new HashSet<Guid>(grantedIds));

        TenantTier tenantTier = await ResolveTenantTierAsync(manifest.TenantId, tenantQuotaStore, cancellationToken);
        List<MUiEngineCapability> capabilities = capabilityResolver.BuildCapabilities(tenantTier);

        MUiEngineManifest engineManifest = BuildUiEngineManifest(manifest, tenantTier, capabilities);
        UiEngineManifestContext context = new()
        {
            Manifest = engineManifest,
            TenantTier = tenantTier.ToString(),
            TenantId = manifest.TenantId,
            UserId = userId,
            UserPermissions = userPermissions,
            Services = services ?? EmptyServiceProvider.Instance
        };

        foreach (IUiEngineManifestContributor contributor in _contributors)
        {
            await contributor.ContributeAsync(context, cancellationToken);
        }

        await EnrichManifestV2FromCatalogAsync(engineManifest, services, cancellationToken);
        ApplyCapabilityGuards(engineManifest, capabilities);
        SortManifest(engineManifest);
        return engineManifest;
    }

    private static async Task<TenantTier> ResolveTenantTierAsync(
        string? tenantId,
        ITenantQuotaStore? tenantQuotaStore,
        CancellationToken cancellationToken)
    {
        string? effectiveTenantId = string.IsNullOrWhiteSpace(tenantId) ? TenantContext.CurrentTenantId : tenantId;
        if (string.IsNullOrWhiteSpace(effectiveTenantId) || tenantQuotaStore is null)
        {
            return TenantTier.Free;
        }

        TenantQuota? quota = await tenantQuotaStore.GetQuotaAsync(effectiveTenantId, cancellationToken);
        return quota?.Tier ?? TenantTier.Free;
    }

    private static MUiEngineManifest BuildUiEngineManifest(
        MUiManifest manifest,
        TenantTier tenantTier,
        IReadOnlyList<MUiEngineCapability> capabilities)
    {
        List<MUiManifestItem> allItems = [.. manifest.Groups
            .SelectMany(x => x.Items)
            .SelectMany(FlattenUiManifestItems)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.UiKey, StringComparer.OrdinalIgnoreCase)];

        Dictionary<string, MUiManifestItem> byUiKey = allItems
            .Where(x => !string.IsNullOrWhiteSpace(x.UiKey))
            .GroupBy(x => x.UiKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        Dictionary<string, string> screenKeyByUiKey = allItems
            .Where(x => x.Type is PermissionType.Menu or PermissionType.Tab)
            .ToDictionary(
                x => x.UiKey,
                x => MUiEngineKeyBuilder.BuildScreenKey(x.UiKey),
                StringComparer.OrdinalIgnoreCase);

        MUiEngineManifest engineManifest = new()
        {
            UserId = manifest.UserId,
            TenantId = manifest.TenantId,
            GeneratedAtUtc = manifest.GeneratedAtUtc,
            LicenseTier = tenantTier.ToString(),
            Capabilities = [.. capabilities],
            AppShell = new MUiEngineAppShell
            {
                RootLayout = "m-shell-default",
                Slots = ["header", "sidebar", "content", "footer"],
                Theme = "default"
            },
            AuthProfile = new MUiEngineAuthProfile
            {
                TokenSource = "header",
                TokenKey = "Authorization",
                RefreshPath = "/api/v1/auth/refresh-token",
                TenantHeaderKey = "X-Tenant-Id",
                CorrelationIdKey = "X-Correlation-Id",
                FailurePolicy = "401"
            },
            GenerationHints = new MUiEngineGenerationHints
            {
                OutputBasePath = "./src/generated/ui-engine",
                CoreOutputPath = "./src/generated/ui-engine/core",
                ApiOutputPath = "./src/generated/ui-engine/api",
                ModelsOutputPath = "./src/generated/ui-engine/models",
                FeaturesOutputPath = "./src/generated/ui-engine/features",
                WatchEnabled = true
            }
        };

        foreach (MUiManifestGroup group in manifest.Groups.OrderBy(x => x.GroupDisplayName, StringComparer.OrdinalIgnoreCase))
        {
            engineManifest.NavigationGroups.Add(new MUiEngineNavigationGroup
            {
                GroupName = group.GroupName,
                GroupDisplayName = group.GroupDisplayName,
                Items = [.. group.Items
                    .OrderBy(x => x.Order)
                    .ThenBy(x => x.UiKey, StringComparer.OrdinalIgnoreCase)
                    .Select(x => BuildNavigationNode(x, screenKeyByUiKey))]
            });
        }

        foreach (MUiManifestItem screenItem in allItems.Where(x => x.Type is PermissionType.Menu or PermissionType.Tab))
        {
            string screenKey = screenKeyByUiKey[screenItem.UiKey];
            string dataSourceKey = MUiEngineKeyBuilder.BuildDataSourceKey(screenItem.UiKey);
            List<string> actionKeys = CollectActionKeys(screenItem);
            string? requiredCapability = ResolveRequiredCapability(screenItem.UiKey);

            MUiEngineComponent mainComponent = new()
            {
                ComponentKey = MUiEngineKeyBuilder.BuildComponentKey(screenItem.UiKey, "main"),
                UiKey = screenItem.UiKey,
                ScreenKey = screenKey,
                ComponentType = screenItem.Type == PermissionType.Tab ? "tab-content" : "page-content",
                RequiredCapability = requiredCapability,
                Slot = "main",
                Order = 0,
                DataSourceKey = dataSourceKey,
                ActionKeys = actionKeys,
                Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["renderMode"] = "metadata-first",
                    ["permissionName"] = screenItem.PermissionName
                }
            };

            engineManifest.Screens.Add(new MUiEngineScreen
            {
                ScreenKey = screenKey,
                UiKey = screenItem.UiKey,
                Title = screenItem.DisplayName,
                Route = screenItem.Route,
                RequiredCapability = requiredCapability,
                IsVisible = screenItem.IsVisible,
                IsEnabled = screenItem.IsEnabled,
                DisabledReason = screenItem.DisabledReason,
                DataSourceKey = dataSourceKey,
                ActionKeys = actionKeys,
                Components = [mainComponent]
            });

            engineManifest.DataSources.Add(new MUiEngineDataSource
            {
                DataSourceKey = dataSourceKey,
                UiKey = screenItem.UiKey,
                ScreenKey = screenKey,
                EndpointPath = BuildDefaultEndpointPath(screenItem.Route),
                HttpMethod = "GET",
                ResponseModel = $"{ToPascalCase(screenItem.UiKey)}Response"
            });
        }

        foreach (MUiManifestItem actionItem in allItems.Where(x => x.Type == PermissionType.Action))
        {
            string? targetScreenKey = ResolveTargetScreenKey(actionItem, byUiKey, screenKeyByUiKey);
            string? requiredCapability = ResolveRequiredCapability(actionItem.UiKey);

            engineManifest.Actions.Add(new MUiEngineAction
            {
                ActionKey = MUiEngineKeyBuilder.BuildActionKey(actionItem.UiKey),
                UiKey = actionItem.UiKey,
                PermissionName = actionItem.PermissionName,
                Label = actionItem.DisplayName,
                Route = actionItem.Route,
                ActionType = actionItem.Route == "/" ? "command" : "navigate",
                RequiredCapability = requiredCapability,
                IsVisible = actionItem.IsVisible,
                IsEnabled = actionItem.IsEnabled,
                DisabledReason = actionItem.DisabledReason,
                TargetScreenKey = targetScreenKey
            });
        }

        return engineManifest;
    }

    private static async Task EnrichManifestV2FromCatalogAsync(
        MUiEngineManifest manifest,
        IServiceProvider? services,
        CancellationToken cancellationToken)
    {
        if (services is null)
        {
            return;
        }

        if (manifest.ApiContracts is { Count: > 0 } && manifest.RuleBindings is { Count: > 0 })
        {
            return;
        }

        ICatalogScanService? catalogScanService = services.GetService(typeof(ICatalogScanService)) as ICatalogScanService;
        if (catalogScanService is null)
        {
            return;
        }

        if (manifest.ApiContracts is not { Count: > 0 })
        {
            IReadOnlyList<MUiEngineCatalogApiDescriptor> apis = await catalogScanService.ScanApisAsync(cancellationToken);
            manifest.ApiContracts = [.. apis.Select(x => new MUiEngineApiContract
            {
                OperationId = $"{x.ControllerName}_{x.ActionName}",
                EndpointPath = x.Route,
                HttpMethod = x.HttpMethod,
                RequestSchemaRef = x.RequestType,
                ResponseSchemaRef = x.ResponseType,
                Tags = [x.ControllerName]
            })];
        }

        if (manifest.RuleBindings is not { Count: > 0 })
        {
            IReadOnlyList<MUiEngineCatalogBinding> bindings = await catalogScanService.BuildBindingsAsync(cancellationToken);
            manifest.RuleBindings = [.. bindings.Select(x => new MUiEngineRuleBinding
            {
                EndpointRoute = x.EndpointRoute,
                WorkflowName = x.WorkflowName,
                ContextType = x.ContextType,
                OrderedRules = [.. x.Rules.OrderBy(r => r.Order).Select(r => r.Code)]
            })];
        }
    }

    private static void SortManifest(MUiEngineManifest manifest)
    {
        manifest.Screens = [.. manifest.Screens
            .OrderBy(x => x.Route, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ScreenKey, StringComparer.OrdinalIgnoreCase)];

        manifest.Actions = [.. manifest.Actions
            .OrderBy(x => x.Route, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ActionKey, StringComparer.OrdinalIgnoreCase)];

        manifest.DataSources = [.. manifest.DataSources
            .OrderBy(x => x.EndpointPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.DataSourceKey, StringComparer.OrdinalIgnoreCase)];
    }

    private static void ApplyCapabilityGuards(MUiEngineManifest manifest, IReadOnlyList<MUiEngineCapability> capabilities)
    {
        Dictionary<string, MUiEngineCapability> capabilityByKey = capabilities.ToDictionary(x => x.CapabilityKey, StringComparer.OrdinalIgnoreCase);
        HashSet<string> disabledScreenKeys = new(StringComparer.OrdinalIgnoreCase);

        foreach (MUiEngineScreen screen in manifest.Screens)
        {
            if (string.IsNullOrWhiteSpace(screen.RequiredCapability) ||
                !capabilityByKey.TryGetValue(screen.RequiredCapability, out MUiEngineCapability? capability) ||
                capability.IsEnabled)
            {
                continue;
            }

            disabledScreenKeys.Add(screen.ScreenKey);
            screen.IsEnabled = false;
            screen.DisabledReason ??= $"tier_requires_{capability.RequiredTier.ToLowerInvariant()}";

            foreach (MUiEngineComponent component in screen.Components.Where(x =>
                         string.Equals(x.RequiredCapability, screen.RequiredCapability, StringComparison.OrdinalIgnoreCase)))
            {
                if (capability.ComponentOverrides.TryGetValue(component.ComponentType, out string? overrideType))
                {
                    component.ComponentType = overrideType;
                    continue;
                }

                component.ComponentType = "mu-upgrade-prompt";
                component.Props["requiredTier"] = capability.RequiredTier;
                component.Props["capabilityKey"] = capability.CapabilityKey;
            }
        }

        foreach (MUiEngineNavigationGroup group in manifest.NavigationGroups)
        {
            ApplyCapabilityGuardToNodes(group.Items, disabledScreenKeys);
        }

        foreach (MUiEngineAction action in manifest.Actions)
        {
            if (string.IsNullOrWhiteSpace(action.RequiredCapability) ||
                !capabilityByKey.TryGetValue(action.RequiredCapability, out MUiEngineCapability? capability) ||
                capability.IsEnabled)
            {
                continue;
            }

            if (capability.ActionOverrides.TryGetValue(action.UiKey, out _)
                || capability.ActionOverrides.TryGetValue(action.ActionKey, out _))
            {
                action.ActionType = "navigate";
                action.Route = "/account/upgrade";
                action.IsEnabled = true;
                action.DisabledReason = null;
                continue;
            }

            action.IsEnabled = false;
            action.DisabledReason ??= $"tier_requires_{capability.RequiredTier.ToLowerInvariant()}";
        }
    }

    private static void ApplyCapabilityGuardToNodes(
        List<MUiEngineNavigationNode> nodes,
        IReadOnlySet<string> disabledScreenKeys)
    {
        foreach (MUiEngineNavigationNode node in nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.ScreenKey) && disabledScreenKeys.Contains(node.ScreenKey))
            {
                node.IsEnabled = false;
                node.DisabledReason ??= "tier_upgrade_required";
            }

            ApplyCapabilityGuardToNodes(node.Children, disabledScreenKeys);
        }
    }

    private static string? ResolveRequiredCapability(string? uiKey)
    {
        if (string.IsNullOrWhiteSpace(uiKey))
        {
            return null;
        }

        string normalized = uiKey.ToLowerInvariant();
        if (normalized.Contains("decision", StringComparison.Ordinal))
        {
            return "decision-table";
        }

        if (normalized.Contains("nrules", StringComparison.Ordinal))
        {
            return "nrules";
        }

        if (normalized.Contains("cep", StringComparison.Ordinal))
        {
            return "cep";
        }

        if (normalized.Contains("feel", StringComparison.Ordinal))
        {
            return "feel-playground";
        }

        if (normalized.Contains("rule", StringComparison.Ordinal))
        {
            return "rule-engine";
        }

        return null;
    }

    private static List<MUiManifestItem> FlattenUiManifestItems(MUiManifestItem root)
    {
        List<MUiManifestItem> result = [root];
        foreach (MUiManifestItem child in root.Children)
        {
            result.AddRange(FlattenUiManifestItems(child));
        }

        return result;
    }

    private static MUiEngineNavigationNode BuildNavigationNode(
        MUiManifestItem item,
        IReadOnlyDictionary<string, string> screenKeyByUiKey)
    {
        return new MUiEngineNavigationNode
        {
            NodeKey = MUiEngineKeyBuilder.BuildNodeKey(item.UiKey),
            UiKey = item.UiKey,
            ParentUiKey = item.ParentUiKey,
            Title = item.DisplayName,
            Route = item.Route,
            Type = item.Type,
            Icon = item.Icon,
            Order = item.Order,
            IsVisible = item.IsVisible,
            IsEnabled = item.IsEnabled,
            DisabledReason = item.DisabledReason,
            ScreenKey = screenKeyByUiKey.TryGetValue(item.UiKey, out string? screenKey) ? screenKey : null,
            ActionKeys = CollectActionKeys(item),
            Children = [.. item.Children
                .OrderBy(x => x.Order)
                .ThenBy(x => x.UiKey, StringComparer.OrdinalIgnoreCase)
                .Select(x => BuildNavigationNode(x, screenKeyByUiKey))]
        };
    }

    private static List<string> CollectActionKeys(MUiManifestItem item)
    {
        return [.. FlattenUiManifestItems(item)
            .Where(x => x.Type == PermissionType.Action)
            .Select(x => MUiEngineKeyBuilder.BuildActionKey(x.UiKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
    }

    private static string? ResolveTargetScreenKey(
        MUiManifestItem actionItem,
        Dictionary<string, MUiManifestItem> byUiKey,
        Dictionary<string, string> screenKeyByUiKey)
    {
        string? parentUiKey = actionItem.ParentUiKey;
        while (!string.IsNullOrWhiteSpace(parentUiKey))
        {
            if (screenKeyByUiKey.TryGetValue(parentUiKey, out string? screenKey))
            {
                return screenKey;
            }

            if (!byUiKey.TryGetValue(parentUiKey, out MUiManifestItem? parent))
            {
                break;
            }

            parentUiKey = parent.ParentUiKey;
        }

        return null;
    }

    private static string BuildDefaultEndpointPath(string route)
    {
        if (string.IsNullOrWhiteSpace(route) || route == "/")
        {
            return "/api/v1";
        }

        return $"/api/v1{route.ToLowerInvariant()}";
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Ui";
        }

        string[] parts = value
            .Replace(".", " ", StringComparison.Ordinal)
            .Replace("_", " ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        StringBuilder builder = new();
        foreach (string part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
            {
                builder.Append(part[1..].ToLowerInvariant());
            }
        }

        return builder.Length == 0 ? "Ui" : builder.ToString();
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();

        public object? GetService(Type serviceType)
        {
            _ = serviceType;
            return null;
        }
    }
}
