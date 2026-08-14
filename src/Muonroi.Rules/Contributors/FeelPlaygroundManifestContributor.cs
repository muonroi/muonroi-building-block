namespace Muonroi.Rules.Contributors;

/// <summary>
/// Contributes FEEL playground components and actions to the UI engine manifest.
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
public sealed class FeelPlaygroundManifestContributor : IUiEngineManifestContributor
{
    /// <inheritdoc />
    public int Order => 130;
    /// <inheritdoc />
    public string ModuleId => "feel-playground";
    /// <inheritdoc />
    public string RequiredTier => "Starter";

    /// <inheritdoc />
    public Task ContributeAsync(UiEngineManifestContext context, CancellationToken ct = default)
    {
        _ = ct;
        MUiEngineManifest manifest = context.Manifest;

        const string uiKey = "rule.engine.feel";
        const string capabilityKey = "feel-playground";
        string screenKey = MUiEngineKeyBuilder.BuildScreenKey(uiKey);
        string dataSourceKey = MUiEngineKeyBuilder.BuildDataSourceKey(uiKey);
        string evaluateActionKey = MUiEngineKeyBuilder.BuildActionKey("rule.engine.feel.evaluate");

        manifest.ComponentRegistry.Components["feel-playground"] = new MUiEngineComponentDescriptor
        {
            ComponentType = "feel-playground",
            BundleUrl = "/assets/muonroi-rule-components.iife.js",
            CssUrl = "/assets/muonroi-rule-components.css",
            CustomElementTag = "mu-feel-playground",
            IsLazyLoaded = true,
            RequiredTier = RequiredTier
        };

        if (!manifest.Screens.Any(x => string.Equals(x.ScreenKey, screenKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Screens.Add(new MUiEngineScreen
            {
                ScreenKey = screenKey,
                UiKey = uiKey,
                Title = "FEEL Playground",
                Route = "/rule-engine/feel",
                RequiredCapability = capabilityKey,
                IsVisible = true,
                IsEnabled = true,
                DataSourceKey = dataSourceKey,
                ActionKeys = [evaluateActionKey],
                Components =
                [
                    new MUiEngineComponent
                    {
                        ComponentKey = MUiEngineKeyBuilder.BuildComponentKey(uiKey, "main"),
                        UiKey = uiKey,
                        ScreenKey = screenKey,
                        ComponentType = "feel-playground",
                        RequiredCapability = capabilityKey,
                        Slot = "main",
                        Order = 0,
                        DataSourceKey = dataSourceKey,
                        ActionKeys = [evaluateActionKey],
                        Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["evaluateEndpoint"] = "/api/v1/feel/evaluate",
                            ["autocompleteEndpoint"] = "/api/v1/feel/autocomplete",
                            ["examplesEndpoint"] = "/api/v1/feel/examples"
                        }
                    }
                ]
            });
        }

        if (!manifest.Actions.Any(x => string.Equals(x.ActionKey, evaluateActionKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Actions.Add(new MUiEngineAction
            {
                ActionKey = evaluateActionKey,
                UiKey = "rule.engine.feel.evaluate",
                PermissionName = "RuleEngine.Feel.Evaluate",
                Label = "Evaluate FEEL",
                Route = "/api/v1/feel/evaluate",
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
                EndpointPath = "/api/v1/feel/examples",
                HttpMethod = "GET",
                ResponseModel = "FeelExamplesResponse"
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
                NodeKey = MUiEngineKeyBuilder.BuildNodeKey("rule.engine.feel.nav"),
                UiKey = uiKey,
                Title = "FEEL Playground",
                Route = "/rule-engine/feel",
                Type = PermissionType.Menu,
                Order = 40,
                IsVisible = true,
                IsEnabled = true,
                ScreenKey = screenKey,
                ActionKeys = [evaluateActionKey]
            });
        }

        return Task.CompletedTask;
    }
}
