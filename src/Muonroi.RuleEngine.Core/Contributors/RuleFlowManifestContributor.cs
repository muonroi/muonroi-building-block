using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Enums;
using Muonroi.Core.Abstractions.Models;

namespace Muonroi.RuleEngine.Core.Contributors;

/// <summary>
/// Contributes rule flow related metadata to the UI engine manifest.
/// </summary>
public sealed class RuleFlowManifestContributor : IUiEngineManifestContributor
{
    /// <inheritdoc />
    public int Order => 140;
    /// <inheritdoc />
    public string ModuleId => "rule-flow";
    /// <inheritdoc />
    public string RequiredTier => "Enterprise";

    /// <inheritdoc />
    public Task ContributeAsync(UiEngineManifestContext context, CancellationToken ct = default)
    {
        _ = ct;
        MUiEngineManifest manifest = context.Manifest;

        const string uiKey = "rule.engine.flow";
        const string capabilityKey = "rule-flow";
        string screenKey = MUiEngineKeyBuilder.BuildScreenKey(uiKey);
        string dataSourceKey = MUiEngineKeyBuilder.BuildDataSourceKey(uiKey);
        string saveActionKey = MUiEngineKeyBuilder.BuildActionKey("rule.engine.flow.save");

        manifest.ComponentRegistry.Components["rule-flow-designer"] = new MUiEngineComponentDescriptor
        {
            ComponentType = "rule-flow-designer",
            BundleUrl = "/assets/muonroi-rule-components.iife.js",
            CssUrl = "/assets/muonroi-rule-components.css",
            CustomElementTag = "mu-rule-flow-designer",
            IsLazyLoaded = true,
            RequiredTier = RequiredTier
        };

        if (!manifest.Screens.Any(x => string.Equals(x.ScreenKey, screenKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Screens.Add(new MUiEngineScreen
            {
                ScreenKey = screenKey,
                UiKey = uiKey,
                Title = "Rule Flow Designer",
                Route = "/rule-engine/flow",
                RequiredCapability = capabilityKey,
                IsVisible = true,
                IsEnabled = true,
                DataSourceKey = dataSourceKey,
                ActionKeys = [saveActionKey],
                Components =
                [
                    new MUiEngineComponent
                    {
                        ComponentKey = MUiEngineKeyBuilder.BuildComponentKey(uiKey, "main"),
                        UiKey = uiKey,
                        ScreenKey = screenKey,
                        ComponentType = "rule-flow-designer",
                        RequiredCapability = capabilityKey,
                        Slot = "main",
                        Order = 0,
                        DataSourceKey = dataSourceKey,
                        ActionKeys = [saveActionKey]
                    }
                ]
            });
        }

        if (!manifest.Actions.Any(x => string.Equals(x.ActionKey, saveActionKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Actions.Add(new MUiEngineAction
            {
                ActionKey = saveActionKey,
                UiKey = "rule.engine.flow.save",
                PermissionName = "RuleEngine.Flow.Save",
                Label = "Save Rule Flow",
                Route = "/api/v1/rule-engine/flow",
                ActionType = "submit",
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
                EndpointPath = "/api/v1/rule-engine/flow",
                HttpMethod = "GET",
                ResponseModel = "RuleFlowGraphResponse"
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
                NodeKey = MUiEngineKeyBuilder.BuildNodeKey("rule.engine.flow.nav"),
                UiKey = uiKey,
                Title = "Rule Flow",
                Route = "/rule-engine/flow",
                Type = PermissionType.Menu,
                Order = 50,
                IsVisible = true,
                IsEnabled = true,
                ScreenKey = screenKey,
                ActionKeys = [saveActionKey]
            });
        }

        return Task.CompletedTask;
    }
}
