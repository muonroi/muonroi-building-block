using System.Reflection;
using Microsoft.Extensions.Logging;
using Muonroi.RuleEngine.Runtime.Web.Models;

namespace Muonroi.RuleEngine.Runtime.Web.Services;

/// <summary>
/// Default implementation that uses reflection to discover rule context properties
/// and build contract schemas. Consumers can replace this with a custom provider.
/// </summary>
public class MDefaultRuleFlowContractProvider : IMRuleFlowContractProvider
{
    private readonly ILogger<MDefaultRuleFlowContractProvider>? _log;

    /// <summary>
    /// Initializes a new instance of <see cref="MDefaultRuleFlowContractProvider"/>.
    /// </summary>
    public MDefaultRuleFlowContractProvider(ILogger<MDefaultRuleFlowContractProvider>? log = null)
    {
        _log = log;
    }

    /// <inheritdoc />
    public Task<MRuleFlowContractLookupResponse?> MGetContractAsync(
        string sourceType, string sourceCode, CancellationToken ct = default)
    {
        var ruleType = MFindRuleTypeByCode(sourceCode);
        if (ruleType is null)
        {
            _log?.LogDebug("No rule type found for code {Code}", sourceCode);
            return Task.FromResult<MRuleFlowContractLookupResponse?>(null);
        }

        var contextType = MExtractContextType(ruleType);
        var schema = contextType is not null ? MBuildSchemaFromType(contextType) : null;

        return Task.FromResult<MRuleFlowContractLookupResponse?>(
            new MRuleFlowContractLookupResponse(
                sourceType, sourceCode,
                schema is not null ? new MRuleContractSchema($"{sourceCode}.Request", schema) : null,
                null));
    }

    /// <inheritdoc />
    public Task<MRuleFlowContractLookupResponse?> MGetFlowContractAsync(
        string flowCode, CancellationToken ct = default)
    {
        return Task.FromResult<MRuleFlowContractLookupResponse?>(
            new MRuleFlowContractLookupResponse("flow", flowCode, null, null));
    }

    /// <inheritdoc />
    public Task<MRuleFlowNodeContractResponse?> MGetNodeAuthoringContractAsync(
        string flowCode, string nodeId, CancellationToken ct = default)
    {
        return Task.FromResult<MRuleFlowNodeContractResponse?>(
            new MRuleFlowNodeContractResponse(nodeId, flowCode, null, null));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MRuleFlowSummary>> MListFlowsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<MRuleFlowSummary>>(Array.Empty<MRuleFlowSummary>());
    }

    /// <summary>
    /// Find a registered IRule type by its code (static Code field or attribute).
    /// </summary>
    protected virtual Type? MFindRuleTypeByCode(string code)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!type.IsClass || type.IsAbstract) continue;

                    var ruleInterface = type.GetInterfaces()
                        .FirstOrDefault(i => i.IsGenericType &&
                            (i.GetGenericTypeDefinition().FullName?.Contains("IRule") == true));
                    if (ruleInterface is null) continue;

                    var codeField = type.GetField("Code",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    if (codeField is not null && string.Equals(
                        codeField.GetValue(null)?.ToString(), code, StringComparison.OrdinalIgnoreCase))
                    {
                        return type;
                    }
                }
            }
            catch
            {
                // Skip assemblies that can't be scanned
            }
        }

        return null;
    }

    /// <summary>
    /// Extract the context type T from IRule&lt;T&gt;.
    /// </summary>
    protected static Type? MExtractContextType(Type ruleType)
    {
        var ruleInterface = ruleType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                (i.GetGenericTypeDefinition().FullName?.Contains("IRule") == true));
        return ruleInterface?.GetGenericArguments().FirstOrDefault();
    }

    /// <summary>
    /// Build a flat schema from a type's public properties.
    /// </summary>
    protected static IReadOnlyList<MRuleContractField> MBuildSchemaFromType(Type type, int depth = 0)
    {
        if (depth > 3) return Array.Empty<MRuleContractField>();

        var fields = new List<MRuleContractField>();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propType = prop.PropertyType;
            var typeName = MMapClrTypeToSchemaType(propType);
            IReadOnlyList<MRuleContractField>? children = null;

            if (typeName == "object" && !propType.IsPrimitive && propType != typeof(string))
            {
                var elementType = MGetCollectionElementType(propType);
                if (elementType is not null)
                {
                    typeName = "array";
                    children = MBuildSchemaFromType(elementType, depth + 1);
                }
                else
                {
                    children = MBuildSchemaFromType(propType, depth + 1);
                }
            }

            fields.Add(new MRuleContractField(
                prop.Name,
                typeName,
                IsRequired: !MIsNullable(propType),
                Children: children?.Count > 0 ? children : null));
        }

        return fields;
    }

    private static string MMapClrTypeToSchemaType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(string)) return "string";
        if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short))
            return "integer";
        if (underlying == typeof(decimal) || underlying == typeof(double) || underlying == typeof(float))
            return "number";
        if (underlying == typeof(bool)) return "boolean";
        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset))
            return "datetime";
        if (underlying == typeof(Guid)) return "string";
        if (underlying.IsEnum) return "string";
        if (MGetCollectionElementType(underlying) is not null) return "array";
        return "object";
    }

    private static Type? MGetCollectionElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(IList<>) ||
                genDef == typeof(IEnumerable<>) || genDef == typeof(IReadOnlyList<>) ||
                genDef == typeof(ICollection<>) || genDef == typeof(IReadOnlyCollection<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static bool MIsNullable(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
    }
}
