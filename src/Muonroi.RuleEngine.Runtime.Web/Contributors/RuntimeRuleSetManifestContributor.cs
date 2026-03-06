using Muonroi.Core.Abstractions.Enums;

namespace Muonroi.RuleEngine.Runtime.Web.Contributors;

public sealed class RuntimeRuleSetManifestContributor : IUiEngineManifestContributor
{
    public int Order => 140;
    public string ModuleId => "runtime-ruleset";
    public string RequiredTier => "Starter";

    public Task ContributeAsync(UiEngineManifestContext context, CancellationToken ct = default)
    {
        _ = ct;
        MUiEngineManifest manifest = context.Manifest;

        const string listUiKey = "runtime.ruleset.list";
        const string editorUiKey = "runtime.ruleset.editor";
        const string capabilityKey = "rule-engine";

        string listScreenKey = MUiEngineKeyBuilder.BuildScreenKey(listUiKey);
        string editorScreenKey = MUiEngineKeyBuilder.BuildScreenKey(editorUiKey);
        string listDataSourceKey = MUiEngineKeyBuilder.BuildDataSourceKey(listUiKey);
        string editorDataSourceKey = MUiEngineKeyBuilder.BuildDataSourceKey(editorUiKey);

        string viewActionKey = MUiEngineKeyBuilder.BuildActionKey("runtime.ruleset.view");
        string saveActionKey = MUiEngineKeyBuilder.BuildActionKey("runtime.ruleset.save");
        string activateActionKey = MUiEngineKeyBuilder.BuildActionKey("runtime.ruleset.activate");
        string validateActionKey = MUiEngineKeyBuilder.BuildActionKey("runtime.ruleset.validate");
        string exportActionKey = MUiEngineKeyBuilder.BuildActionKey("runtime.ruleset.export");
        string dryRunActionKey = MUiEngineKeyBuilder.BuildActionKey("runtime.ruleset.dryrun");

        manifest.ComponentRegistry.Components["runtime-ruleset-list"] = new MUiEngineComponentDescriptor
        {
            ComponentType = "runtime-ruleset-list",
            BundleUrl = "/assets/muonroi-rule-components.iife.js",
            CssUrl = "/assets/muonroi-rule-components.css",
            CustomElementTag = "mu-runtime-ruleset-list",
            IsLazyLoaded = true,
            RequiredTier = RequiredTier
        };

        manifest.ComponentRegistry.Components["runtime-ruleset-editor"] = new MUiEngineComponentDescriptor
        {
            ComponentType = "runtime-ruleset-editor",
            BundleUrl = "/assets/muonroi-rule-components.iife.js",
            CssUrl = "/assets/muonroi-rule-components.css",
            CustomElementTag = "mu-runtime-ruleset-editor",
            IsLazyLoaded = true,
            RequiredTier = RequiredTier
        };

        if (!manifest.Screens.Any(x => string.Equals(x.ScreenKey, listScreenKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Screens.Add(new MUiEngineScreen
            {
                ScreenKey = listScreenKey,
                UiKey = listUiKey,
                Title = "Runtime Rulesets",
                Route = "/rule-engine/runtime-rulesets",
                RequiredCapability = capabilityKey,
                IsVisible = true,
                IsEnabled = true,
                DataSourceKey = listDataSourceKey,
                ActionKeys = [viewActionKey],
                Components =
                [
                    new MUiEngineComponent
                    {
                        ComponentKey = MUiEngineKeyBuilder.BuildComponentKey(listUiKey, "main"),
                        UiKey = listUiKey,
                        ScreenKey = listScreenKey,
                        ComponentType = "runtime-ruleset-list",
                        RequiredCapability = capabilityKey,
                        Slot = "main",
                        Order = 0,
                        DataSourceKey = listDataSourceKey,
                        ActionKeys = [viewActionKey],
                        Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["apiBase"] = "/api/v1/rule-engine/rulesets",
                            ["editorRoute"] = "/rule-engine/runtime-rulesets/editor"
                        }
                    }
                ]
            });
        }

        if (!manifest.Screens.Any(x => string.Equals(x.ScreenKey, editorScreenKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Screens.Add(new MUiEngineScreen
            {
                ScreenKey = editorScreenKey,
                UiKey = editorUiKey,
                Title = "Runtime Ruleset Editor",
                Route = "/rule-engine/runtime-rulesets/editor",
                RequiredCapability = capabilityKey,
                IsVisible = true,
                IsEnabled = true,
                DataSourceKey = editorDataSourceKey,
                ActionKeys = [saveActionKey, activateActionKey, validateActionKey, exportActionKey, dryRunActionKey],
                Components =
                [
                    new MUiEngineComponent
                    {
                        ComponentKey = MUiEngineKeyBuilder.BuildComponentKey(editorUiKey, "main"),
                        UiKey = editorUiKey,
                        ScreenKey = editorScreenKey,
                        ComponentType = "runtime-ruleset-editor",
                        RequiredCapability = capabilityKey,
                        Slot = "main",
                        Order = 0,
                        DataSourceKey = editorDataSourceKey,
                        ActionKeys = [saveActionKey, activateActionKey, validateActionKey, exportActionKey, dryRunActionKey],
                        Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["apiBase"] = "/api/v1/rule-engine/rulesets",
                            ["validateEndpointTemplate"] = "/api/v1/rule-engine/rulesets/{workflow}/validate",
                            ["activateEndpointTemplate"] = "/api/v1/rule-engine/rulesets/{workflow}/activate/{version}",
                            ["exportEndpointTemplate"] = "/api/v1/rule-engine/rulesets/{workflow}/export",
                            ["dryRunEndpointTemplate"] = "/api/v1/rule-engine/rulesets/{workflow}/dry-run",
                            ["auditEndpointTemplate"] = "/api/v1/rule-engine/rulesets/{workflow}/audit"
                        }
                    }
                ]
            });
        }

        EnsureAction(manifest, new MUiEngineAction
        {
            ActionKey = viewActionKey,
            UiKey = "runtime.ruleset.view",
            PermissionName = "RuleSets.View",
            Label = "View Runtime Rulesets",
            Route = "/rule-engine/runtime-rulesets",
            ActionType = "navigate",
            RequiredCapability = capabilityKey,
            IsVisible = true,
            IsEnabled = true,
            TargetScreenKey = listScreenKey
        });

        EnsureAction(manifest, new MUiEngineAction
        {
            ActionKey = saveActionKey,
            UiKey = "runtime.ruleset.save",
            PermissionName = "RuleSets.Update",
            Label = "Save Runtime Ruleset",
            Route = "/api/v1/rule-engine/rulesets/{workflow}",
            ActionType = "submit",
            RequiredCapability = capabilityKey,
            IsVisible = true,
            IsEnabled = true,
            TargetScreenKey = editorScreenKey
        });

        EnsureAction(manifest, new MUiEngineAction
        {
            ActionKey = activateActionKey,
            UiKey = "runtime.ruleset.activate",
            PermissionName = "RuleSets.Activate",
            Label = "Activate Ruleset",
            Route = "/api/v1/rule-engine/rulesets/{workflow}/activate/{version}",
            ActionType = "command",
            RequiredCapability = capabilityKey,
            IsVisible = true,
            IsEnabled = true,
            TargetScreenKey = editorScreenKey
        });

        EnsureAction(manifest, new MUiEngineAction
        {
            ActionKey = validateActionKey,
            UiKey = "runtime.ruleset.validate",
            PermissionName = "RuleSets.Validate",
            Label = "Validate Ruleset",
            Route = "/api/v1/rule-engine/rulesets/{workflow}/validate",
            ActionType = "command",
            RequiredCapability = capabilityKey,
            IsVisible = true,
            IsEnabled = true,
            TargetScreenKey = editorScreenKey
        });

        EnsureAction(manifest, new MUiEngineAction
        {
            ActionKey = exportActionKey,
            UiKey = "runtime.ruleset.export",
            PermissionName = "RuleSets.Export",
            Label = "Export Ruleset",
            Route = "/api/v1/rule-engine/rulesets/{workflow}/export",
            ActionType = "download",
            RequiredCapability = capabilityKey,
            IsVisible = true,
            IsEnabled = true,
            TargetScreenKey = editorScreenKey
        });

        EnsureAction(manifest, new MUiEngineAction
        {
            ActionKey = dryRunActionKey,
            UiKey = "runtime.ruleset.dryrun",
            PermissionName = "RuleSets.Execute",
            Label = "Dry Run Ruleset",
            Route = "/api/v1/rule-engine/rulesets/{workflow}/dry-run",
            ActionType = "submit",
            RequiredCapability = capabilityKey,
            IsVisible = true,
            IsEnabled = true,
            TargetScreenKey = editorScreenKey
        });

        EnsureDataSource(manifest, new MUiEngineDataSource
        {
            DataSourceKey = listDataSourceKey,
            UiKey = listUiKey,
            ScreenKey = listScreenKey,
            EndpointPath = "/api/v1/rule-engine/rulesets",
            HttpMethod = "GET",
            ResponseModel = "RuntimeRuleSetListResponse"
        });

        EnsureDataSource(manifest, new MUiEngineDataSource
        {
            DataSourceKey = editorDataSourceKey,
            UiKey = editorUiKey,
            ScreenKey = editorScreenKey,
            EndpointPath = "/api/v1/rule-engine/rulesets/{workflow}/export",
            HttpMethod = "GET",
            ResponseModel = "RuntimeRuleSetResponse"
        });

        MUiEngineNavigationGroup? group = manifest.NavigationGroups.FirstOrDefault(x =>
            string.Equals(x.GroupName, "rule-engine", StringComparison.OrdinalIgnoreCase));
        if (group is null)
        {
            group = new MUiEngineNavigationGroup
            {
                GroupName = "rule-engine",
                GroupDisplayName = "Rule Engine"
            };
            manifest.NavigationGroups.Add(group);
        }

        if (!group.Items.Any(x => string.Equals(x.ScreenKey, listScreenKey, StringComparison.OrdinalIgnoreCase)))
        {
            group.Items.Add(new MUiEngineNavigationNode
            {
                NodeKey = MUiEngineKeyBuilder.BuildNodeKey("runtime.ruleset.nav"),
                UiKey = listUiKey,
                Title = "Runtime Rulesets",
                Route = "/rule-engine/runtime-rulesets",
                Type = PermissionType.Menu,
                Order = 15,
                IsVisible = true,
                IsEnabled = true,
                ScreenKey = listScreenKey,
                ActionKeys = [viewActionKey],
                Children = []
            });
        }

        group.Items = [.. group.Items.OrderBy(x => x.Order).ThenBy(x => x.UiKey, StringComparer.OrdinalIgnoreCase)];
        return Task.CompletedTask;
    }

    private static void EnsureAction(MUiEngineManifest manifest, MUiEngineAction action)
    {
        if (!manifest.Actions.Any(x => string.Equals(x.ActionKey, action.ActionKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Actions.Add(action);
        }
    }

    private static void EnsureDataSource(MUiEngineManifest manifest, MUiEngineDataSource source)
    {
        if (!manifest.DataSources.Any(x => string.Equals(x.DataSourceKey, source.DataSourceKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.DataSources.Add(source);
        }
    }
}
