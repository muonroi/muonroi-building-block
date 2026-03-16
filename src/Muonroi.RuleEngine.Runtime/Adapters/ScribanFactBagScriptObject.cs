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
            string key = NormalizeKey(kv.Key);
            this[key] = ConvertValue(kv.Value);
        }
    }

    private static object? ConvertValue(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement jsonElement => ConvertJsonElement(jsonElement),
            IDictionary<string, object?> dict => ConvertDictionary(dict),
            IReadOnlyDictionary<string, object?> roDict => ConvertReadOnlyDictionary(roDict),
            IEnumerable<object?> list when value is not string => ConvertList(list),
            _ => value
        };
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
        // Scriban uses snake_case by default; preserve original keys
        // by replacing dots with underscores (FactBag nested keys like "order.total")
        return key.Replace('.', '_');
    }
}
