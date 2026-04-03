using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.UiEngine.Catalog.Attributes;
using Muonroi.UiEngine.Catalog.Models;
using System.CodeDom.Compiler;
using System.Globalization;
using System.Reflection;
using RuntimeRuleOptions = Muonroi.RuleEngine.Runtime.Rules.RuleOptions;

namespace Muonroi.UiEngine.Catalog.Services;

/// <summary>
/// Default catalog scanner for UI engine APIs and rules.
/// </summary>
public sealed class CatalogScanService(
    IApiDescriptionGroupCollectionProvider apiDescriptions,
    IServiceProvider serviceProvider,
    IMLog<CatalogScanService> logger,
    IOptionsMonitor<RuntimeRuleOptions>? runtimeRuleOptions = null,
    ISystemExecutionContextAccessor? executionContextAccessor = null) : ICatalogScanService
{
    private readonly ISystemExecutionContextAccessor _executionContextAccessor =
        executionContextAccessor ?? new SystemExecutionContextAccessor();

    /// <inheritdoc/>
    public Task<IReadOnlyList<MUiEngineCatalogApiDescriptor>> ScanApisAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<MUiEngineCatalogApiDescriptor> descriptors = [];

        foreach (ApiDescription api in apiDescriptions.ApiDescriptionGroups.Items.SelectMany(group => group.Items))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ControllerActionDescriptor? action = api.ActionDescriptor as ControllerActionDescriptor;
            IEnumerable<object> metadata = action?.EndpointMetadata ?? [];

            List<IAuthorizeData> authorizeData = [.. metadata.OfType<IAuthorizeData>()];
            bool allowAnonymous = metadata.OfType<IAllowAnonymous>().Any();
            bool allowAnonymousWithoutTenant = metadata.Any(x =>
                string.Equals(x.GetType().Name, "AllowAnonymousWithoutTenantAttribute", StringComparison.OrdinalIgnoreCase));

            ApiParameterDescription? bodyParameter = api.ParameterDescriptions.FirstOrDefault(x =>
                x.Source == BindingSource.Body || string.Equals(x.Source?.Id, "Body", StringComparison.OrdinalIgnoreCase));

            ApiResponseType? successResponse = api.SupportedResponseTypes
                .Where(x => x.StatusCode is >= 200 and < 300)
                .OrderBy(x => x.StatusCode)
                .FirstOrDefault();

            string route = NormalizeRoute(api.RelativePath);
            string method = (api.HttpMethod ?? "GET").ToUpperInvariant();
            string controllerName = action?.ControllerName
                ?? TryGetRouteValue(api.ActionDescriptor.RouteValues, "controller");
            string actionName = action?.ActionName
                ?? TryGetRouteValue(api.ActionDescriptor.RouteValues, "action");
            if (string.IsNullOrWhiteSpace(controllerName) || string.IsNullOrWhiteSpace(actionName))
            {
                logger.Debug(
                    "CatalogScan missing route values. Route={Route}, Method={Method}, Controller={Controller}, Action={Action}, DisplayName={DisplayName}",
                    route,
                    method,
                    controllerName,
                    actionName,
                    api.ActionDescriptor.DisplayName);
            }

            descriptors.Add(new MUiEngineCatalogApiDescriptor
            {
                Route = route,
                HttpMethod = method,
                ControllerName = controllerName,
                ActionName = actionName,
                RequestType = bodyParameter?.Type?.FullName ?? bodyParameter?.ModelMetadata?.ModelType?.FullName,
                ResponseType = successResponse?.Type?.FullName,
                AuthSchemes = CollectCsvValues(authorizeData.Select(x => x.AuthenticationSchemes)),
                Policies = CollectCsvValues(authorizeData.Select(x => x.Policy)),
                RequiresTenantId = !allowAnonymous && !allowAnonymousWithoutTenant
            });
        }

        IReadOnlyList<MUiEngineCatalogApiDescriptor> result = [.. descriptors
            .OrderBy(x => x.Route, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.HttpMethod, StringComparer.OrdinalIgnoreCase)];
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MUiEngineCatalogRuleDescriptor>> ScanRulesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<MUiEngineCatalogRuleDescriptor> descriptors = [];

        Type ruleOpenGeneric = typeof(IRule<>);
        Type compensatableOpenGeneric = typeof(ICompensatableRule<>);
        string? tenantId = NormalizeTenantId(_executionContextAccessor.Get().TenantId);

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (Type type in GetLoadableTypes(assembly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                Type? ruleInterface = type.GetInterfaces()
                    .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == ruleOpenGeneric);
                if (ruleInterface is null)
                {
                    continue;
                }

                object? instance = TryCreateRuleInstance(type);
                string code = ReadStringProperty(instance, "Code") ?? type.Name;
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                int order = ReadIntProperty(instance, "Order") ?? 0;
                IReadOnlyList<string> dependsOn = ReadStringListProperty(instance, "DependsOn");
                string hookPoint = ReadPropertyString(instance, "HookPoint") ?? "BeforeRule";
                string ruleType = ReadPropertyString(instance, "Type") ?? "Validation";
                string contextType = ruleInterface.GenericTypeArguments[0].FullName ?? ruleInterface.GenericTypeArguments[0].Name;

                descriptors.Add(new MUiEngineCatalogRuleDescriptor
                {
                    Code = code,
                    Name = type.Name,
                    Order = order,
                    DependsOn = [.. dependsOn],
                    HookPoint = hookPoint,
                    RuleType = ruleType,
                    ContextType = contextType,
                    Source = ResolveSource(type),
                    IsEnabled = ResolveIsEnabled(code, tenantId),
                    IsCompensatable = type.GetInterfaces().Any(x =>
                        x.IsGenericType && x.GetGenericTypeDefinition() == compensatableOpenGeneric)
                });
            }
        }

        IReadOnlyList<MUiEngineCatalogRuleDescriptor> deduped = [.. descriptors
            .GroupBy(x => $"{x.ContextType}::{x.Code}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(x => x.Order).First())
            .OrderBy(x => x.ContextType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Order)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)];

        return Task.FromResult(deduped);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MUiEngineCatalogBinding>> BuildBindingsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MUiEngineCatalogApiDescriptor> apis = await ScanApisAsync(cancellationToken);
        IReadOnlyList<MUiEngineCatalogRuleDescriptor> rules = await ScanRulesAsync(cancellationToken);

        Dictionary<string, List<MUiEngineCatalogRuleDescriptor>> rulesByContext = rules
            .GroupBy(x => x.ContextType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderBy(r => r.Order).ThenBy(r => r.Code, StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        Dictionary<(string route, string method), (string? ContextType, string? WorkflowName)> boundContexts = BuildBoundContextsByAction();
        List<MUiEngineCatalogBinding> bindings = [];

        foreach (MUiEngineCatalogApiDescriptor api in apis)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? contextType = null;
            string? workflowName = null;
            if (boundContexts.TryGetValue((api.Route, api.HttpMethod), out (string? ContextType, string? WorkflowName) mapped))
            {
                contextType = mapped.ContextType;
                workflowName = mapped.WorkflowName;
            }

            contextType ??= ResolveContextByConvention(api.Route, rulesByContext.Keys);
            List<MUiEngineCatalogRuleRef> ruleRefs = [];
            if (!string.IsNullOrWhiteSpace(contextType) && rulesByContext.TryGetValue(contextType, out List<MUiEngineCatalogRuleDescriptor>? contextRules))
            {
                ruleRefs = [.. contextRules.Select(rule => new MUiEngineCatalogRuleRef
                {
                    Code = rule.Code,
                    Order = rule.Order,
                    HookPoint = rule.HookPoint
                })];
            }

            bindings.Add(new MUiEngineCatalogBinding
            {
                EndpointRoute = api.Route,
                HttpMethod = api.HttpMethod,
                ContextType = contextType,
                WorkflowName = workflowName ?? ResolveWorkflowName(api.Route),
                Rules = ruleRefs
            });
        }

        return [.. bindings
            .OrderBy(x => x.EndpointRoute, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.HttpMethod, StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc/>
    public async Task<MUiEngineCatalogGraph> BuildGraphAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MUiEngineCatalogBinding> bindings = await BuildBindingsAsync(cancellationToken);
        IReadOnlyList<MUiEngineCatalogRuleDescriptor> rules = await ScanRulesAsync(cancellationToken);

        Dictionary<string, MUiEngineCatalogGraphNode> nodes = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> edgeKeys = new(StringComparer.OrdinalIgnoreCase);
        List<MUiEngineCatalogGraphEdge> edges = [];

        foreach (MUiEngineCatalogBinding binding in bindings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string endpointNodeId = BuildEndpointNodeId(binding.HttpMethod, binding.EndpointRoute);
            nodes[endpointNodeId] = new MUiEngineCatalogGraphNode
            {
                Id = endpointNodeId,
                Type = "endpoint",
                Label = $"{binding.HttpMethod} {binding.EndpointRoute}",
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["route"] = binding.EndpointRoute,
                    ["httpMethod"] = binding.HttpMethod,
                    ["contextType"] = binding.ContextType,
                    ["workflowName"] = binding.WorkflowName
                }
            };

            foreach (MUiEngineCatalogRuleRef rule in binding.Rules)
            {
                string ruleNodeId = BuildRuleNodeId(rule.Code);
                if (!nodes.ContainsKey(ruleNodeId))
                {
                    nodes[ruleNodeId] = new MUiEngineCatalogGraphNode
                    {
                        Id = ruleNodeId,
                        Type = "rule",
                        Label = rule.Code,
                        Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["code"] = rule.Code,
                            ["order"] = rule.Order.ToString(CultureInfo.InvariantCulture),
                            ["hookPoint"] = rule.HookPoint
                        }
                    };
                }

                AddEdge(edges, edgeKeys, endpointNodeId, ruleNodeId, "triggers");
                AddEdge(edges, edgeKeys, ruleNodeId, endpointNodeId, "part-of");
            }
        }

        HashSet<string> knownRuleCodes = [.. rules.Select(x => x.Code)];
        foreach (MUiEngineCatalogRuleDescriptor rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string ruleNodeId = BuildRuleNodeId(rule.Code);
            if (!nodes.ContainsKey(ruleNodeId))
            {
                nodes[ruleNodeId] = new MUiEngineCatalogGraphNode
                {
                    Id = ruleNodeId,
                    Type = "rule",
                    Label = rule.Code,
                    Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["code"] = rule.Code,
                        ["contextType"] = rule.ContextType,
                        ["source"] = rule.Source,
                        ["hookPoint"] = rule.HookPoint
                    }
                };
            }

            foreach (string dependency in rule.DependsOn)
            {
                if (!knownRuleCodes.Contains(dependency))
                {
                    continue;
                }

                AddEdge(edges, edgeKeys, BuildRuleNodeId(dependency), ruleNodeId, "depends-on");
            }
        }

        return new MUiEngineCatalogGraph
        {
            Nodes = [.. nodes.Values.OrderBy(x => x.Type, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)],
            Edges = [.. edges.OrderBy(x => x.From, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.To, StringComparer.OrdinalIgnoreCase)]
        };
    }

    private Dictionary<(string route, string method), (string? ContextType, string? WorkflowName)> BuildBoundContextsByAction()
    {
        Dictionary<(string route, string method), (string? ContextType, string? WorkflowName)> mappings = [];

        foreach (ApiDescription api in apiDescriptions.ApiDescriptionGroups.Items.SelectMany(group => group.Items))
        {
            ControllerActionDescriptor? action = api.ActionDescriptor as ControllerActionDescriptor;
            BindRuleContextAttribute? attribute = action?.EndpointMetadata?.OfType<BindRuleContextAttribute>().FirstOrDefault();
            if (attribute is null)
            {
                continue;
            }

            string route = NormalizeRoute(api.RelativePath);
            string method = (api.HttpMethod ?? "GET").ToUpperInvariant();
            mappings[(route, method)] = (
                attribute.ContextType.FullName ?? attribute.ContextType.Name,
                string.IsNullOrWhiteSpace(attribute.WorkflowName) ? null : attribute.WorkflowName.Trim());
        }

        return mappings;
    }

    private bool ResolveIsEnabled(string ruleCode, string? tenantId)
    {
        if (runtimeRuleOptions?.CurrentValue is null)
        {
            return true;
        }

        RuntimeRuleOptions options = runtimeRuleOptions.CurrentValue;
        if (options.RuleToggles.TryGetValue(ruleCode, out bool globalEnabled) && !globalEnabled)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(tenantId) &&
            options.TenantRuleToggles.TryGetValue(tenantId, out Dictionary<string, bool>? tenantToggles) &&
            tenantToggles.TryGetValue(ruleCode, out bool tenantEnabled))
        {
            return tenantEnabled;
        }

        return true;
    }

    private object? TryCreateRuleInstance(Type ruleType)
    {
        try
        {
            return ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, ruleType);
        }
        catch
        {
            try
            {
                return Activator.CreateInstance(ruleType, nonPublic: true);
            }
            catch
            {
                return null;
            }
        }
    }

    private static string ResolveSource(Type ruleType)
    {
        string namespaceValue = ruleType.Namespace ?? string.Empty;
        bool hasGeneratedCodeAttribute = ruleType.GetCustomAttributes<GeneratedCodeAttribute>(inherit: true).Any();

        if (hasGeneratedCodeAttribute ||
            namespaceValue.Contains(".Generated", StringComparison.OrdinalIgnoreCase) ||
            namespaceValue.Contains(".RuleGen", StringComparison.OrdinalIgnoreCase))
        {
            return "rulegen";
        }

        if (namespaceValue.Contains(".DecisionTable", StringComparison.OrdinalIgnoreCase) ||
            namespaceValue.Contains(".Runtime", StringComparison.OrdinalIgnoreCase) ||
            namespaceValue.Contains(".Rulesets", StringComparison.OrdinalIgnoreCase))
        {
            return "runtime-json";
        }

        return "code-first";
    }

    private static string? ResolveContextByConvention(string route, IEnumerable<string> contextTypes)
    {
        string normalizedRoute = route.Trim('/').ToLowerInvariant();
        (string ContextType, string Token)? best = null;

        foreach (string contextType in contextTypes.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            string token = BuildContextToken(contextType);
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (!normalizedRoute.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (best is null || token.Length > best.Value.Token.Length)
            {
                best = (contextType, token);
            }
        }

        return best?.ContextType;
    }

    private static string BuildContextToken(string contextType)
    {
        string simpleName = contextType.Split('.').LastOrDefault() ?? contextType;
        if (simpleName.EndsWith("Context", StringComparison.OrdinalIgnoreCase))
        {
            simpleName = simpleName[..^"Context".Length];
        }

        return simpleName.Trim().ToLowerInvariant();
    }

    private static string? ResolveWorkflowName(string route)
    {
        if (route.Contains("/rule-engine/rulesets", StringComparison.OrdinalIgnoreCase))
        {
            return "runtime-ruleset";
        }

        if (route.Contains("/decision-table", StringComparison.OrdinalIgnoreCase))
        {
            return "decision-table";
        }

        if (route.Contains("/rule-engine/nrules", StringComparison.OrdinalIgnoreCase))
        {
            return "nrules";
        }

        if (route.Contains("/rule-engine/cep", StringComparison.OrdinalIgnoreCase))
        {
            return "cep";
        }

        return null;
    }

    private static void AddEdge(
        ICollection<MUiEngineCatalogGraphEdge> edges,
        ISet<string> keys,
        string from,
        string to,
        string edgeType)
    {
        string key = $"{from}|{to}|{edgeType}";
        if (!keys.Add(key))
        {
            return;
        }

        edges.Add(new MUiEngineCatalogGraphEdge
        {
            From = from,
            To = to,
            EdgeType = edgeType
        });
    }

    private static string BuildEndpointNodeId(string method, string route)
    {
        return $"endpoint:{method.ToUpperInvariant()}:{route.ToLowerInvariant()}";
    }

    private static string BuildRuleNodeId(string code)
    {
        return $"rule:{code.ToLowerInvariant()}";
    }

    private static string NormalizeRoute(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return "/";
        }

        string route = relativePath.Trim();
        int queryIndex = route.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            route = route[..queryIndex];
        }

        route = "/" + route.Trim('/');
        while (route.Contains("//", StringComparison.Ordinal))
        {
            route = route.Replace("//", "/", StringComparison.Ordinal);
        }

        return route;
    }

    private static string TryGetRouteValue(IDictionary<string, string?>? routeValues, string key)
    {
        if (routeValues is null)
        {
            return string.Empty;
        }

        return routeValues.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : string.Empty;
    }

    private static List<string> CollectCsvValues(IEnumerable<string?> values)
    {
        return [.. values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(x => x!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
    }

    private static string? ReadStringProperty(object? instance, string propertyName)
    {
        if (instance is null)
        {
            return null;
        }

        PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        object? value = property?.GetValue(instance);
        return value?.ToString();
    }

    private static int? ReadIntProperty(object? instance, string propertyName)
    {
        if (instance is null)
        {
            return null;
        }

        PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        object? value = property?.GetValue(instance);
        if (value is int intValue)
        {
            return intValue;
        }

        if (value is null)
        {
            return null;
        }

        return int.TryParse(value.ToString(), out int parsed) ? parsed : null;
    }

    private static string? ReadPropertyString(object? instance, string propertyName)
    {
        if (instance is null)
        {
            return null;
        }

        PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        object? value = property?.GetValue(instance);
        return value?.ToString();
    }

    private static IReadOnlyList<string> ReadStringListProperty(object? instance, string propertyName)
    {
        if (instance is null)
        {
            return [];
        }

        PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        object? value = property?.GetValue(instance);
        if (value is IEnumerable<string> stringEnumerable)
        {
            return [.. stringEnumerable
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        return [];
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(x => x is not null)!;
        }
        catch
        {
            return [];
        }
    }

    private static string? NormalizeTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
    }
}
