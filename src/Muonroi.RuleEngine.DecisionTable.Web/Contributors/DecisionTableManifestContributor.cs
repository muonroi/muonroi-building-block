using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Enums;
using Muonroi.Core.Abstractions.Models;

namespace Muonroi.RuleEngine.DecisionTable.Web.Contributors;

public sealed class DecisionTableManifestContributor : IUiEngineManifestContributor
{
    public int Order => 100;
    public string ModuleId => "decision-table";
    public string RequiredTier => "Professional";

    public Task ContributeAsync(UiEngineManifestContext context, CancellationToken ct = default)
    {
        _ = ct;
        MUiEngineManifest manifest = context.Manifest;

        const string listUiKey = "decision.table.list";
        const string editorUiKey = "decision.table.editor";
        const string capabilityKey = "decision-table";

        string listScreenKey = MUiEngineKeyBuilder.BuildScreenKey(listUiKey);
        string editorScreenKey = MUiEngineKeyBuilder.BuildScreenKey(editorUiKey);
        string listDataSourceKey = MUiEngineKeyBuilder.BuildDataSourceKey(listUiKey);
        string editorDataSourceKey = MUiEngineKeyBuilder.BuildDataSourceKey(editorUiKey);

        string createActionKey = MUiEngineKeyBuilder.BuildActionKey("decision.table.create");
        string saveActionKey = MUiEngineKeyBuilder.BuildActionKey("decision.table.save");
        string validateActionKey = MUiEngineKeyBuilder.BuildActionKey("decision.table.validate");
        string exportActionKey = MUiEngineKeyBuilder.BuildActionKey("decision.table.export");
        string importActionKey = MUiEngineKeyBuilder.BuildActionKey("decision.table.import");

        manifest.ComponentRegistry.Components["decision-table-editor"] = new MUiEngineComponentDescriptor
        {
            ComponentType = "decision-table-editor",
            BundleUrl = "/assets/muonroi-rule-components.iife.js",
            CssUrl = "/assets/muonroi-rule-components.css",
            CustomElementTag = "mu-decision-table",
            IsLazyLoaded = true,
            RequiredTier = RequiredTier
        };

        manifest.ComponentRegistry.Components["decision-table-list"] = new MUiEngineComponentDescriptor
        {
            ComponentType = "decision-table-list",
            BundleUrl = "/assets/muonroi-rule-components.iife.js",
            CssUrl = "/assets/muonroi-rule-components.css",
            CustomElementTag = "mu-decision-table-list",
            IsLazyLoaded = true,
            RequiredTier = RequiredTier
        };

        if (!manifest.Screens.Any(x => string.Equals(x.ScreenKey, listScreenKey, StringComparison.OrdinalIgnoreCase)))
        {
            manifest.Screens.Add(new MUiEngineScreen
            {
                ScreenKey = listScreenKey,
                UiKey = listUiKey,
                Title = "Decision Tables",
                Route = "/decision-tables",
                RequiredCapability = capabilityKey,
                IsVisible = true,
                IsEnabled = true,
                DataSourceKey = listDataSourceKey,
                ActionKeys = [createActionKey],
                Components =
                [
                    new MUiEngineComponent
                    {
                        ComponentKey = MUiEngineKeyBuilder.BuildComponentKey(listUiKey, "main"),
                        UiKey = listUiKey,
                        ScreenKey = listScreenKey,
                        ComponentType = "decision-table-list",
                        RequiredCapability = capabilityKey,
                        Slot = "main",
                        Order = 0,
                        DataSourceKey = listDataSourceKey,
                        ActionKeys = [createActionKey],
                        Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["apiBase"] = "/api/v1/decision-tables",
                            ["editorRoute"] = "/decision-tables/editor"
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
                Title = "Decision Table Editor",
                Route = "/decision-tables/editor",
                RequiredCapability = capabilityKey,
                IsVisible = true,
                IsEnabled = true,
                DataSourceKey = editorDataSourceKey,
                ActionKeys = [saveActionKey, validateActionKey, exportActionKey, importActionKey],
                Components =
                [
                    new MUiEngineComponent
                    {
                        ComponentKey = MUiEngineKeyBuilder.BuildComponentKey(editorUiKey, "main"),
                        UiKey = editorUiKey,
                        ScreenKey = editorScreenKey,
                        ComponentType = "decision-table-editor",
                        RequiredCapability = capabilityKey,
                        Slot = "main",
                        Order = 0,
                        DataSourceKey = editorDataSourceKey,
                        ActionKeys = [saveActionKey, validateActionKey, exportActionKey, importActionKey],
                        Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["apiBase"] = "/api/v1/decision-tables",
                            ["validateEndpoint"] = "/api/v1/decision-tables/{id}/validate",
                            ["exportEndpoint"] = "/api/v1/decision-tables/{id}/export/{format}",
                            ["feelEndpoint"] = "/api/v1/feel/autocomplete",
                            ["historyEndpoint"] = "/api/v1/decision-tables/{id}/versions",
                            ["historyVersionEndpoint"] = "/api/v1/decision-tables/{id}/versions/{v}",
                            ["diffEndpoint"] = "/api/v1/decision-tables/{id}/versions/{v1}/diff/{v2}",
                            ["auditEndpoint"] = "/api/v1/decision-tables/{id}/audit",
                            ["reorderEndpoint"] = "/api/v1/decision-tables/{id}/rows/reorder",
                            ["executeEndpoint"] = "/api/v1/decision-tables/{id}/execute",
                            ["maxRows"] = "1000",
                            ["enableExcelImport"] = "true",
                            ["enableVersionDiff"] = "true"
                        }
                    }
                ]
            });
        }

        EnsureAction(manifest, new MUiEngineAction
        {
            ActionKey = createActionKey,
            UiKey = "decision.table.create",
            PermissionName = "DecisionTables.Create",
            Label = "Create Decision Table",
            Route = "/decision-tables/editor",
            ActionType = "navigate",
            RequiredCapability = capabilityKey,
            IsVisible = true,
            IsEnabled = true,
            TargetScreenKey = editorScreenKey
        });

        EnsureAction(manifest, new MUiEngineAction
        {
            ActionKey = saveActionKey,
            UiKey = "decision.table.save",
            PermissionName = "DecisionTables.Update",
            Label = "Save Decision Table",
            Route = "/api/v1/decision-tables",
            ActionType = "submit",
            RequiredCapability = capabilityKey,
            IsVisible = true,
            IsEnabled = true,
            TargetScreenKey = editorScreenKey
        });

        EnsureAction(manifest, new MUiEngineAction
        {
            ActionKey = validateActionKey,
            UiKey = "decision.table.validate",
            PermissionName = "DecisionTables.Validate",
            Label = "Validate Decision Table",
            Route = "/api/v1/decision-tables/{id}/validate",
            ActionType = "command",
            RequiredCapability = capabilityKey,
            IsVisible = true,
            IsEnabled = true,
            TargetScreenKey = editorScreenKey
        });

        EnsureAction(manifest, new MUiEngineAction
        {
            ActionKey = exportActionKey,
            UiKey = "decision.table.export",
            PermissionName = "DecisionTables.Export",
            Label = "Export Decision Table",
            Route = "/api/v1/decision-tables/{id}/export/dmn",
            ActionType = "download",
            RequiredCapability = capabilityKey,
            IsVisible = true,
            IsEnabled = true,
            TargetScreenKey = editorScreenKey
        });

        EnsureAction(manifest, new MUiEngineAction
        {
            ActionKey = importActionKey,
            UiKey = "decision.table.import",
            PermissionName = "DecisionTables.Import",
            Label = "Import Decision Table",
            Route = "/api/v1/decision-tables/import",
            ActionType = "upload",
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
            EndpointPath = "/api/v1/decision-tables",
            HttpMethod = "GET",
            ResponseModel = "DecisionTableListResponse"
        });

        EnsureDataSource(manifest, new MUiEngineDataSource
        {
            DataSourceKey = editorDataSourceKey,
            UiKey = editorUiKey,
            ScreenKey = editorScreenKey,
            EndpointPath = "/api/v1/decision-tables/{id}",
            HttpMethod = "GET",
            ResponseModel = "DecisionTableResponse"
        });

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

        if (!navigationGroup.Items.Any(x => string.Equals(x.ScreenKey, listScreenKey, StringComparison.OrdinalIgnoreCase)))
        {
            navigationGroup.Items.Add(new MUiEngineNavigationNode
            {
                NodeKey = MUiEngineKeyBuilder.BuildNodeKey("decision.table.nav"),
                UiKey = listUiKey,
                Title = "Decision Tables",
                Route = "/decision-tables",
                Type = PermissionType.Menu,
                Order = 10,
                IsVisible = true,
                IsEnabled = true,
                ScreenKey = listScreenKey,
                ActionKeys = [createActionKey],
                Children = []
            });
        }

        navigationGroup.Items = [.. navigationGroup.Items
            .OrderBy(x => x.Order)
            .ThenBy(x => x.UiKey, StringComparer.OrdinalIgnoreCase)];

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
