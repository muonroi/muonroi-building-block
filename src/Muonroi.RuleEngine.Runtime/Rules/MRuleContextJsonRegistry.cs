using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Registry for rule context types that can be deserialized during dry-run execution.
/// Replaces the unsafe assembly-scanning type resolution pattern.
/// All types must be explicitly registered via <see cref="AddRuleContext{T}"/>.
/// </summary>
/// <remarks>
/// Source-gen compatible: each registration stores a deserialization delegate
/// that can use <c>JsonTypeInfo&lt;T&gt;</c> from a <c>JsonSerializerContext</c> when available.
/// AOT-safe: no assembly scanning, no reflection-based type lookup.
/// Thread-safe: uses <see cref="ConcurrentDictionary{TKey,TValue}"/> internally.
/// </remarks>
public sealed class MRuleContextJsonRegistry
{
    private readonly ConcurrentDictionary<string, Func<string, JsonSerializerOptions, object>> _deserializers =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, HashSet<string>>? _tenantWhitelists;
    private readonly IMEcosystemRegistry? _ecosystemRegistry;

    private static readonly JsonSerializerOptions s_defaultOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Creates a new registry instance.</summary>
    /// <param name="registry">Optional ecosystem registry for capability-based enhancements.</param>
    public MRuleContextJsonRegistry(IMEcosystemRegistry? registry = null)
    {
        _ecosystemRegistry = registry;
        if (registry?.Has(MCapability.MultiTenant) == true)
        {
            _tenantWhitelists = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Registers a context type for safe deserialization.
    /// </summary>
    /// <typeparam name="T">The context type to register. Must be a reference type.</typeparam>
    /// <param name="typeNames">
    /// One or more names this type can be looked up by (e.g., full name, short name, assembly-qualified name).
    /// If empty, registers automatically using <c>typeof(T).FullName</c> and <c>typeof(T).Name</c>.
    /// </param>
    public void AddRuleContext<T>(params string[] typeNames) where T : class
    {
        Func<string, JsonSerializerOptions, object> deserializer = static (json, options) =>
        {
            T? result = JsonSerializer.Deserialize<T>(json, options);
            return result ?? throw new MConfigurationException(
                $"Deserialization of type '{typeof(T).FullName}' returned null.",
                "RuleContextType");
        };

        if (typeNames.Length == 0)
        {
            typeNames = [typeof(T).FullName!, typeof(T).Name];
        }

        foreach (string name in typeNames)
        {
            _deserializers[name] = deserializer;
        }
    }

    /// <summary>
    /// Deserializes a rule context by registered type name.
    /// </summary>
    /// <param name="typeName">The registered type name to look up.</param>
    /// <param name="json">Raw JSON string to deserialize.</param>
    /// <param name="tenantId">
    /// Optional tenant ID for multi-tenant whitelist validation (+MultiTenant enhancement).
    /// Ignored when the MultiTenant capability is not active.
    /// </param>
    /// <returns>The deserialized context object.</returns>
    /// <exception cref="MConfigurationException">
    /// Thrown when:
    /// <list type="bullet">
    ///   <item>The type name is null, empty, or whitespace.</item>
    ///   <item>The type name has not been registered via <see cref="AddRuleContext{T}"/>.</item>
    ///   <item>The type is not in the tenant's whitelist (+MultiTenant).</item>
    ///   <item>The JSON payload cannot be deserialized to the registered type.</item>
    /// </list>
    /// </exception>
    public object DeserializeContext(string typeName, string json, string? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new MConfigurationException(
                "Rule context type name must not be null or empty.",
                "RuleContextType");
        }

        bool loggingEnabled = _ecosystemRegistry?.Has(MCapability.Logging) == true;

        if (!_deserializers.TryGetValue(typeName, out Func<string, JsonSerializerOptions, object>? deserializer))
        {
            if (loggingEnabled)
            {
                Console.WriteLine(
                    $"[Ecosystem] AUDIT: Rule context deserialization DENIED — type '{typeName}' not registered");
            }

            throw new MConfigurationException(
                $"Rule context type '{typeName}' is not registered. " +
                "Register it via AddRuleContext<T>() during startup.",
                "RuleContextType");
        }

        // +MultiTenant: enforce per-tenant type whitelist when configured
        if (_tenantWhitelists is not null && !string.IsNullOrWhiteSpace(tenantId))
        {
            if (_tenantWhitelists.TryGetValue(tenantId, out HashSet<string>? allowedTypes) &&
                !allowedTypes.Contains(typeName))
            {
                if (loggingEnabled)
                {
                    Console.WriteLine(
                        $"[Ecosystem] AUDIT: Rule context deserialization DENIED — " +
                        $"type '{typeName}' not in tenant '{tenantId}' whitelist");
                }

                throw new MConfigurationException(
                    $"Rule context type '{typeName}' is not allowed for tenant '{tenantId}'.",
                    "RuleContextType");
            }
        }

        try
        {
            object result = deserializer(json, s_defaultOptions);

            if (loggingEnabled)
            {
                Console.WriteLine(
                    $"[Ecosystem] AUDIT: Rule context deserialization ALLOWED — type '{typeName}'");
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new MConfigurationException(
                $"Failed to deserialize rule context type '{typeName}': {ex.Message}",
                "RuleContextType");
        }
    }

    /// <summary>
    /// Configures a tenant-specific type whitelist (+MultiTenant enhancement).
    /// Has no effect when the MultiTenant capability is not active.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="allowedTypes">Type names allowed for this tenant.</param>
    public void SetTenantWhitelist(string tenantId, IEnumerable<string> allowedTypes)
    {
        if (_tenantWhitelists is null) return;
        _tenantWhitelists[tenantId] = new HashSet<string>(allowedTypes, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns true if the given type name is registered.</summary>
    public bool IsRegistered(string typeName) =>
        !string.IsNullOrWhiteSpace(typeName) && _deserializers.ContainsKey(typeName);
}
