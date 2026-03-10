using Muonroi.RuleEngine.Abstractions.Authoring;
using System.Collections.Concurrent;
using System.Reflection;

namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class MRuleAuthoringManifestRegistry(IServiceProvider? serviceProvider = null)
{
    private readonly IServiceProvider? _serviceProvider = serviceProvider;
    private readonly ConcurrentDictionary<string, MRuleAuthoringManifest> _cache = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<MRuleAuthoringManifest> GetManifests()
    {
        List<MRuleAuthoringManifest> manifests = [];
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
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

    public MRuleAuthoringManifest? GetManifest(Assembly assembly)
    {
        string key = assembly.FullName ?? assembly.GetName().Name ?? Guid.NewGuid().ToString("N");
        return _cache.GetOrAdd(key, _ => BuildManifest(assembly));
    }

    private MRuleAuthoringManifest BuildManifest(Assembly assembly)
    {
        foreach (Type providerType in assembly.GetTypes()
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
        foreach (Type type in assembly.GetTypes().Where(type => !type.IsAbstract && !type.IsInterface))
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

            rules.Add(new MRuleAuthoringEntry
            {
                Code = code,
                Order = order,
                DependsOn = dependsOn,
                ContextTypeName = contextType.FullName,
                ContextSchema = BuildReflectionSchema(contextType)
            });
        }

        return new MRuleAuthoringManifest
        {
            AssemblyName = assembly.GetName().Name ?? "Unknown.Assembly",
            AssemblyVersion = assembly.GetName().Version?.ToString() ?? "1.0.0.0",
            Rules = rules
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
