using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Abstractions.Authoring;
using System.Collections.Concurrent;
using System.Reflection;

namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Discovers rule authoring manifests from loaded assemblies and falls back to reflection when no generated provider exists.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MRuleAuthoringManifestRegistry"/> class.
/// </remarks>
/// <param name="serviceProvider">The optional service provider used to create manifest providers and rules.</param>
/// <param name="assemblies">The optional assembly set to inspect instead of the current app domain.</param>
public sealed class MRuleAuthoringManifestRegistry(IServiceProvider? serviceProvider = null, IEnumerable<Assembly>? assemblies = null)
{
    private readonly IServiceProvider? _serviceProvider = serviceProvider;
    private readonly IReadOnlyList<Assembly>? _assemblies = assemblies?.ToArray();
    private readonly ConcurrentDictionary<string, MRuleAuthoringManifest> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the manifests discovered from the configured assembly set.
    /// </summary>
    /// <returns>The ordered manifest list.</returns>
    public IReadOnlyList<MRuleAuthoringManifest> GetManifests()
    {
        List<MRuleAuthoringManifest> manifests = [];
        foreach (Assembly assembly in GetAssemblies())
        {
            MRuleAuthoringManifest? manifest = GetManifest(assembly);
            if (manifest is not null)
            {
                manifests.Add(manifest);
            }
        }

        return manifests
            .GroupBy(manifest => manifest.AssemblyName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(manifest => manifest.AssemblyName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Gets the authoring manifest for a specific assembly.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns>The discovered or generated manifest.</returns>
    public MRuleAuthoringManifest? GetManifest(Assembly assembly)
    {
        string key = assembly.FullName ?? assembly.GetName().Name ?? Guid.NewGuid().ToString("N");
        return _cache.GetOrAdd(key, _ => BuildManifest(assembly));
    }

    private IReadOnlyList<Assembly> GetAssemblies()
    {
        return _assemblies ?? AppDomain.CurrentDomain.GetAssemblies();
    }

    private MRuleAuthoringManifest BuildManifest(Assembly assembly)
    {
        foreach (Type providerType in GetLoadableTypes(assembly)
                     .Where(type => typeof(IRuleAuthoringManifestProvider).IsAssignableFrom(type) &&
                                    !type.IsAbstract &&
                                    !type.IsInterface))
        {
            if (TryCreate(providerType) is IRuleAuthoringManifestProvider provider)
            {
                return provider.GetManifest();
            }
        }

        return BuildReflectionFallback(assembly);
    }

    private MRuleAuthoringManifest BuildReflectionFallback(Assembly assembly)
    {
        List<MRuleAuthoringEntry> rules = [];
        foreach (Type type in GetLoadableTypes(assembly).Where(type => !type.IsAbstract && !type.IsInterface))
        {
            Type? ruleInterface = type.GetInterfaces()
                .FirstOrDefault(candidate => candidate.IsGenericType &&
                                             string.Equals(candidate.GetGenericTypeDefinition().FullName, typeof(IRule<>).FullName, StringComparison.Ordinal));
            if (ruleInterface is null)
            {
                continue;
            }

            object? instance = TryCreate(type);
            if (instance is null)
            {
                continue;
            }

            PropertyInfo? codeProperty = type.GetProperty(nameof(IRule<object>.Code));
            PropertyInfo? orderProperty = type.GetProperty(nameof(IRule<object>.Order));
            PropertyInfo? dependsOnProperty = type.GetProperty(nameof(IRule<object>.DependsOn));
            IRule<object>? boxed = instance as IRule<object>;
            string code = codeProperty?.GetValue(instance)?.ToString() ?? boxed?.Code ?? type.Name;
            int order = orderProperty?.GetValue(instance) as int? ?? boxed?.Order ?? 0;
            string[] dependsOn = dependsOnProperty?.GetValue(instance) as IReadOnlyList<string> is IReadOnlyList<string> list
                ? [.. list]
                : boxed?.DependsOn?.ToArray() ?? [];
            Type contextType = ruleInterface.GetGenericArguments()[0];
            MRuleCatalogEntryAttribute? catalogAttribute = type.GetCustomAttribute<MRuleCatalogEntryAttribute>();

            rules.Add(new MRuleAuthoringEntry
            {
                Code = code,
                Order = order,
                DependsOn = dependsOn,
                ContextTypeName = contextType.FullName,
                ContextSchema = BuildReflectionSchema(contextType),
                DisplayName = catalogAttribute?.DisplayName ?? code,
                Category = catalogAttribute?.Category,
                Icon = catalogAttribute?.Icon,
                Tags = catalogAttribute?.Tags ?? [],
                Description = catalogAttribute?.Description,
                IsPaletteVisible = catalogAttribute?.IsPaletteVisible ?? true
            });
        }

        return new MRuleAuthoringManifest
        {
            AssemblyName = assembly.GetName().Name ?? "Unknown.Assembly",
            AssemblyVersion = assembly.GetName().Version?.ToString() ?? "1.0.0.0",
            Rules = rules
                .OrderBy(rule => rule.Order)
                .ThenBy(rule => rule.Code, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private object? TryCreate(Type type)
    {
        try
        {
            if (_serviceProvider is not null)
            {
                object? resolved = _serviceProvider.GetService(type);
                if (resolved is not null)
                {
                    return resolved;
                }
            }

            return Activator.CreateInstance(type, nonPublic: true);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
        catch
        {
            return [];
        }
    }

    private static MFactSchemaNode BuildReflectionSchema(Type type)
    {
        return new MFactSchemaNode
        {
            TypeName = type.FullName ?? type.Name,
            IsNullable = Nullable.GetUnderlyingType(type) is not null,
            Fields = BuildFields(type, string.Empty, 0)
        };
    }

    private static IReadOnlyList<MFactSchemaField> BuildFields(Type type, string parentPath, int depth)
    {
        if (depth >= 4 || IsPrimitive(type))
        {
            return [];
        }

        Type normalized = Nullable.GetUnderlyingType(type) ?? type;
        if (TryGetEnumerableElementType(normalized, out Type? elementType))
        {
            if (elementType is null || IsPrimitive(elementType))
            {
                return [];
            }

            string nextPath = string.IsNullOrWhiteSpace(parentPath) ? "items" : parentPath + "[]";
            return BuildFields(elementType, nextPath, depth + 1);
        }

        List<MFactSchemaField> fields = [];
        foreach (PropertyInfo property in normalized.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead)
            {
                continue;
            }

            Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            string pathSegment = char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1);
            string path = string.IsNullOrWhiteSpace(parentPath) ? pathSegment : parentPath + "." + pathSegment;
            fields.Add(new MFactSchemaField
            {
                Path = path,
                Label = property.Name,
                DataType = MapDataType(propertyType),
                Required = Nullable.GetUnderlyingType(property.PropertyType) is null && property.PropertyType.IsValueType,
                Children = BuildFields(propertyType, path, depth + 1)
            });
        }

        return fields;
    }

    private static bool IsPrimitive(Type type)
    {
        Type normalized = Nullable.GetUnderlyingType(type) ?? type;
        return normalized.IsPrimitive ||
               normalized == typeof(string) ||
               normalized == typeof(decimal) ||
               normalized == typeof(Guid) ||
               normalized == typeof(DateTime) ||
               normalized == typeof(DateOnly) ||
               normalized == typeof(TimeOnly);
    }

    private static bool TryGetEnumerableElementType(Type type, out Type? elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return true;
        }

        Type? enumerableInterface = type.GetInterfaces()
            .FirstOrDefault(candidate => candidate.IsGenericType &&
                                         candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerableInterface is not null)
        {
            elementType = enumerableInterface.GetGenericArguments()[0];
            return true;
        }

        elementType = null;
        return false;
    }

    private static string MapDataType(Type type)
    {
        Type normalized = Nullable.GetUnderlyingType(type) ?? type;
        if (TryGetEnumerableElementType(normalized, out _))
        {
            return "array";
        }

        if (normalized == typeof(bool))
        {
            return "boolean";
        }

        if (normalized == typeof(DateTime) || normalized == typeof(DateOnly) || normalized == typeof(TimeOnly))
        {
            return "date";
        }

        if (normalized.IsPrimitive || normalized == typeof(decimal))
        {
            return normalized == typeof(char) ? "string" : "number";
        }

        if (normalized == typeof(string) || normalized == typeof(Guid))
        {
            return "string";
        }

        return "object";
    }
}
