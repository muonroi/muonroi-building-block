namespace Muonroi.RuleEngine.CEP.Contributors;

/// <summary>
/// Contributes CEP entries to the UI engine manifest.
/// </summary>
public sealed class CepManifestContributor : IUiEngineManifestContributor
{
    /// <inheritdoc/>
    public int Order => 120;
    /// <inheritdoc/>
    public string ModuleId => "cep";
    /// <inheritdoc/>
    public string RequiredTier => "Professional";

    /// <inheritdoc/>
    public Task ContributeAsync(UiEngineManifestContext context, CancellationToken ct = default)
    {
        _ = ct;
        MUiEngineManifest manifest = context.Manifest;

        const string uiKey = "rule.engine.cep";
        const string capabilityKey = "cep";
        string screenKey = MUiEngineKeyBuilder.BuildScreenKey(uiKey);
        string dataSourceKey = MUiEngineKeyBuilder.BuildDataSourceKey(uiKey);
        string saveActionKey = MUiEngineKeyBuilder.BuildActionKey("rule.engine.cep.save");
        string simulateActionKey = MUiEngineKeyBuilder.BuildActionKey("rule.engine.cep.simulate");

        manifest.ComponentRegistry.Components["cep-window-config"] = new MUiEngineComponentDescriptor
        {
            ComponentType = "cep-window-config",
            BundleUrl = "/assets/muonroi-rule-components.iife.js",
            CssUrl = "/assets/muonroi-rule-components.css",
            CustomElementTag = "mu-cep-window-config",
            IsLazyLoaded = true,
            RequiredTier = RequiredTier
        };

        if (!manifest.Screens.Any(x => string.Equals(x.ScreenKey, screenKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Screens.Add(new MUiEngineScreen
            {
                ScreenKey = screenKey,
                UiKey = uiKey,
                Title = "CEP Config",
                Route = "/rule-engine/cep",
                RequiredCapability = capabilityKey,
                IsVisible = true,
                IsEnabled = true,
                DataSourceKey = dataSourceKey,
                ActionKeys = [saveActionKey, simulateActionKey],
                Components =
                [
                    new MUiEngineComponent
                    {
                        ComponentKey = MUiEngineKeyBuilder.BuildComponentKey(uiKey, "main"),
                        UiKey = uiKey,
                        ScreenKey = screenKey,
                        ComponentType = "cep-window-config",
                        RequiredCapability = capabilityKey,
                        Slot = "main",
                        Order = 0,
                        DataSourceKey = dataSourceKey,
                        ActionKeys = [saveActionKey, simulateActionKey],
                        Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["apiBase"] = "/api/v1/rule-engine/cep",
                            ["windowTypes"] = "Sliding,Tumbling",
                            ["simulateEndpoint"] = "/api/v1/rule-engine/cep/{id}/simulate"
                        }
                    }
                ]
            });
        }

        if (!manifest.Actions.Any(x => string.Equals(x.ActionKey, saveActionKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Actions.Add(new MUiEngineAction
            {
                ActionKey = saveActionKey,
                UiKey = "rule.engine.cep.save",
                PermissionName = "RuleEngine.Cep.Update",
                Label = "Save CEP Config",
                Route = "/api/v1/rule-engine/cep/{id}",
                ActionType = "submit",
                RequiredCapability = capabilityKey,
                IsVisible = true,
                IsEnabled = true,
                TargetScreenKey = screenKey
            });
        }

        if (!manifest.Actions.Any(x => string.Equals(x.ActionKey, simulateActionKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Actions.Add(new MUiEngineAction
            {
                ActionKey = simulateActionKey,
                UiKey = "rule.engine.cep.simulate",
                PermissionName = "RuleEngine.Cep.Simulate",
                Label = "Simulate CEP",
                Route = "/api/v1/rule-engine/cep/{id}/simulate",
                ActionType = "command",
                RequiredCapability = capabilityKey,
                IsVisible = true,
                IsEnabled = true,
                TargetScreenKey = screenKey
            });
        }

        if (!manifest.DataSources.Any(x => string.Equals(x.DataSourceKey, dataSourceKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.DataSources.Add(new MUiEngineDataSource
            {
                DataSourceKey = dataSourceKey,
                UiKey = uiKey,
                ScreenKey = screenKey,
                EndpointPath = "/api/v1/rule-engine/cep",
                HttpMethod = "GET",
                ResponseModel = "CepConfigListResponse"
            });
        }

        MUiEngineNavigationGroup? navigationGroup = manifest.NavigationGroups.FirstOrDefault(x =>
            string.Equals(x.GroupName, "rule-engine", StringComparison.OrdinalIgnoreCase));
        if (navigationGroup is null)
        {
            navigationGroup = new MUiEngineNavigationGroup
            {
                GroupName = "rule-engine",
                GroupDisplayName = "Rule Engine"
            };
            manifest.NavigationGroups.Add(navigationGroup);
        }

        if (!navigationGroup.Items.Any(x => string.Equals(x.ScreenKey, screenKey, StringComparison.OrdinalIgnoreCase)))
        {
            navigationGroup.Items.Add(new MUiEngineNavigationNode
            {
                NodeKey = MUiEngineKeyBuilder.BuildNodeKey("rule.engine.cep.nav"),
                UiKey = uiKey,
                Title = "CEP",
                Route = "/rule-engine/cep",
                Type = PermissionType.Menu,
                Order = 30,
                IsVisible = true,
                IsEnabled = true,
                ScreenKey = screenKey,
                ActionKeys = [saveActionKey, simulateActionKey]
            });
        }

        return Task.CompletedTask;
    }
}
