using System.Text.Json;
using Scriban.Runtime;

namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Wraps a merged variables dictionary as a Scriban <see cref="ScriptObject"/>
/// so that FactBag keys and context projections are accessible in templates.
/// Handles nested dictionaries and JsonElement conversion.
/// </summary>
internal sealed class ScribanFactBagScriptObject : ScriptObject
{
    public ScribanFactBagScriptObject(IDictionary<string, object?> variables)
    {
        foreach (KeyValuePair<string, object?> kv in variables)
        {
            string lowerKey = kv.Key.ToLowerInvariant();

            // Dotted keys like "container.totalCount" → nest as container: { totalcount: value }
            // Skip internal graph keys (__graph.node.*) — keep flat with underscore replacement
            if (lowerKey.Contains('.') && !lowerKey.StartsWith("__"))
            {
                SetNestedValue(this, lowerKey, ConvertValue(kv.Value));
            }
            else
            {
                this[lowerKey.Replace('.', '_')] = ConvertValue(kv.Value);
            }
        }
    }

    private static void SetNestedValue(ScriptObject root, string dottedKey, object? value)
    {
        string[] parts = dottedKey.Split('.');
        ScriptObject current = root;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            string part = parts[i];
            if (current.TryGetValue(part, out object? existing) && existing is ScriptObject nested)
            {
                current = nested;
            }
            else
            {
                ScriptObject newObj = new();
                current[part] = newObj;
                current = newObj;
            }
        }

        current[parts[^1]] = value;
    }

    private static object? ConvertValue(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            bool or int or long or float or double or decimal => value,
            JsonElement jsonElement => ConvertJsonElement(jsonElement),
            IDictionary<string, object?> dict => ConvertDictionary(dict),
            IReadOnlyDictionary<string, object?> roDict => ConvertReadOnlyDictionary(roDict),
            IEnumerable<object?> list => ConvertList(list),
            _ => ConvertPoco(value, 0)
        };
    }

    private static ScriptObject ConvertPoco(object obj, int depth = 0)
    {
        ScriptObject result = new();
        if (depth > 3) return result; // prevent stack overflow on deep/circular object graphs

        foreach (System.Reflection.PropertyInfo prop in obj.GetType()
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            try
            {
                object? val = prop.GetValue(obj);
                result[NormalizeKey(prop.Name)] = val switch
                {
                    null => null,
                    string s => s,
                    bool or int or long or float or double or decimal => val,
                    JsonElement je => ConvertJsonElement(je),
                    IDictionary<string, object?> d => ConvertDictionary(d),
                    IReadOnlyDictionary<string, object?> rod => ConvertReadOnlyDictionary(rod),
                    IEnumerable<object?> list => ConvertList(list),
                    _ when depth < 3 => ConvertPoco(val, depth + 1),
                    _ => val.ToString()
                };
            }
            catch
            {
                // skip inaccessible properties
            }
        }

        return result;
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Array => ConvertJsonArray(element),
            JsonValueKind.Object => ConvertJsonObject(element),
            _ => element.ToString()
        };
    }

    private static ScriptArray ConvertJsonArray(JsonElement array)
    {
        ScriptArray result = [];
        foreach (JsonElement item in array.EnumerateArray())
        {
            result.Add(ConvertJsonElement(item));
        }
        return result;
    }

    private static ScriptObject ConvertJsonObject(JsonElement obj)
    {
        ScriptObject result = new();
        foreach (JsonProperty prop in obj.EnumerateObject())
        {
            result[NormalizeKey(prop.Name)] = ConvertJsonElement(prop.Value);
        }
        return result;
    }

    private static ScriptObject ConvertDictionary(IDictionary<string, object?> dict)
    {
        ScriptObject result = new();
        foreach (KeyValuePair<string, object?> kv in dict)
        {
            result[NormalizeKey(kv.Key)] = ConvertValue(kv.Value);
        }
        return result;
    }

    private static ScriptObject ConvertReadOnlyDictionary(IReadOnlyDictionary<string, object?> dict)
    {
        ScriptObject result = new();
        foreach (KeyValuePair<string, object?> kv in dict)
        {
            result[NormalizeKey(kv.Key)] = ConvertValue(kv.Value);
        }
        return result;
    }

    private static ScriptArray ConvertList(IEnumerable<object?> list)
    {
        ScriptArray result = [];
        foreach (object? item in list)
        {
            result.Add(ConvertValue(item));
        }
        return result;
    }

    private static string NormalizeKey(string key)
    {
        // Scriban member access is case-sensitive by default.
        // Lowercase so PascalCase context properties match lowercase template references.
        return key.ToLowerInvariant();
    }
}
