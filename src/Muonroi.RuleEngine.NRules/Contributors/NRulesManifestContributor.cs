using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Enums;
using Muonroi.Core.Abstractions.Models;

namespace Muonroi.RuleEngine.NRules.Contributors;

[Obsolete("Frozen: Use Muonroi.RuleEngine.Runtime instead. NRules integration is no longer actively developed.")]
public sealed class NRulesManifestContributor : IUiEngineManifestContributor
{
    public int Order => 110;
    public string ModuleId => "nrules";
    public string RequiredTier => "Professional";

    public Task ContributeAsync(UiEngineManifestContext context, CancellationToken ct = default)
    {
        _ = ct;
        MUiEngineManifest manifest = context.Manifest;
        const string uiKey = "rule.engine.nrules";
        const string capabilityKey = "nrules";
        string screenKey = MUiEngineKeyBuilder.BuildScreenKey(uiKey);
        string dataSourceKey = MUiEngineKeyBuilder.BuildDataSourceKey(uiKey);
        string saveActionKey = MUiEngineKeyBuilder.BuildActionKey("rule.engine.nrules.save");
        string testActionKey = MUiEngineKeyBuilder.BuildActionKey("rule.engine.nrules.test");

        manifest.ComponentRegistry.Components["nrules-editor"] = new MUiEngineComponentDescriptor
        {
            ComponentType = "nrules-editor",
            BundleUrl = "/assets/muonroi-rule-components.iife.js",
            CssUrl = "/assets/muonroi-rule-components.css",
            CustomElementTag = "mu-nrules-editor",
            IsLazyLoaded = true,
            RequiredTier = RequiredTier
        };

        if (!manifest.Screens.Any(x => string.Equals(x.ScreenKey, screenKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Screens.Add(new MUiEngineScreen
            {
                ScreenKey = screenKey,
                UiKey = uiKey,
                Title = "NRules Editor",
                Route = "/rule-engine/nrules",
                RequiredCapability = capabilityKey,
                IsVisible = true,
                IsEnabled = true,
                DataSourceKey = dataSourceKey,
                ActionKeys = [saveActionKey, testActionKey],
                Components =
                [
                    new MUiEngineComponent
                    {
                        ComponentKey = MUiEngineKeyBuilder.BuildComponentKey(uiKey, "main"),
                        UiKey = uiKey,
                        ScreenKey = screenKey,
                        ComponentType = "nrules-editor",
                        RequiredCapability = capabilityKey,
                        Slot = "main",
                        Order = 0,
                        DataSourceKey = dataSourceKey,
                        ActionKeys = [saveActionKey, testActionKey],
                        Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["apiBase"] = "/api/v1/rule-engine/nrules",
                            ["testEndpoint"] = "/api/v1/rule-engine/test",
                            ["maxRules"] = "500"
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
                UiKey = "rule.engine.nrules.save",
                PermissionName = "RuleEngine.NRules.Update",
                Label = "Save NRules",
                Route = "/api/v1/rule-engine/nrules/{id}",
                ActionType = "submit",
                RequiredCapability = capabilityKey,
                IsVisible = true,
                IsEnabled = true,
                TargetScreenKey = screenKey
            });
        }

        if (!manifest.Actions.Any(x => string.Equals(x.ActionKey, testActionKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Actions.Add(new MUiEngineAction
            {
                ActionKey = testActionKey,
                UiKey = "rule.engine.nrules.test",
                PermissionName = "RuleEngine.NRules.Test",
                Label = "Test NRules",
                Route = "/api/v1/rule-engine/test",
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
                EndpointPath = "/api/v1/rule-engine/nrules",
                HttpMethod = "GET",
                ResponseModel = "NRulesDefinitionListResponse"
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
                NodeKey = MUiEngineKeyBuilder.BuildNodeKey("rule.engine.nrules.nav"),
                UiKey = uiKey,
                Title = "NRules",
                Route = "/rule-engine/nrules",
                Type = PermissionType.Menu,
                Order = 20,
                IsVisible = true,
                IsEnabled = true,
                ScreenKey = screenKey,
                ActionKeys = [saveActionKey, testActionKey]
            });
        }

        return Task.CompletedTask;
    }
}
