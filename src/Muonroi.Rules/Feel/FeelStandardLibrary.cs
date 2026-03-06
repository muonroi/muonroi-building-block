using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace Muonroi.Rules.Feel;

public static class FeelStandardLibrary
{
    public static bool TryCall(string functionName, IReadOnlyList<object?> arguments, out object? result)
    {
        string name = Normalize(functionName);
        result = name switch
        {
            "today" => DateTime.UtcNow.Date, // MBB001-exempt: static-class boundary
            "now" => DateTime.UtcNow, // MBB001-exempt: static-class boundary
            "days" => TimeSpan.FromDays(AsDouble(arguments, 0)),
            "years" => TimeSpan.FromDays(AsDouble(arguments, 0) * 365d),
            "date" => ParseDate(arguments),
            "time" => ParseTime(arguments),
            "date_and_time" => ParseDateTime(arguments),
            "duration" => ParseDuration(arguments),
            "years_and_months_duration" => YearsAndMonthsDuration(arguments),
            "days_and_time_duration" => ParseDuration(arguments),
            "list_contains" => ListContains(arguments),
            "count" => Count(arguments),
            "min" => Min(arguments),
            "max" => Max(arguments),
            "sum" => Sum(arguments),
            "mean" => Mean(arguments),
            "all" => All(arguments),
            "any" => Any(arguments),
            "sublist" => Sublist(arguments),
            "append" => Append(arguments),
            "concatenate" => Concat(arguments),
            "insert_before" => InsertBefore(arguments),
            "remove" => Remove(arguments),
            "reverse" => Reverse(arguments),
            "index_of" => IndexOf(arguments),
            "union" => Union(arguments),
            "distinct_values" => DistinctValues(arguments),
            "flatten" => Flatten(arguments),
            "sort" => Sort(arguments),
            "string" => AsString(arguments.ElementAtOrDefault(0)),
            "string_length" => (double)AsString(arguments.ElementAtOrDefault(0)).Length,
            "substring" => Substring(arguments),
            "upper" or "upper_case" => AsString(arguments.ElementAtOrDefault(0)).ToUpperInvariant(),
            "lower" or "lower_case" => AsString(arguments.ElementAtOrDefault(0)).ToLowerInvariant(),
            "substring_before" => SubstringBefore(arguments),
            "substring_after" => SubstringAfter(arguments),
            "replace" => Replace(arguments),
            "contains" => Contains(arguments),
            "starts_with" => StartsWith(arguments),
            "ends_with" => EndsWith(arguments),
            "matches" => Matches(arguments),
            "split" => Split(arguments),
            "decimal" => Decimal(arguments),
            "floor" => Math.Floor(AsDouble(arguments, 0)),
            "ceiling" => Math.Ceiling(AsDouble(arguments, 0)),
            "abs" => Math.Abs(AsDouble(arguments, 0)),
            "modulo" => AsDouble(arguments, 0) % AsDouble(arguments, 1),
            "sqrt" => Math.Sqrt(AsDouble(arguments, 0)),
            "log" => Math.Log(AsDouble(arguments, 0)),
            "exp" => Math.Exp(AsDouble(arguments, 0)),
            "odd" => ((long)AsDouble(arguments, 0)) % 2 != 0,
            "even" => ((long)AsDouble(arguments, 0)) % 2 == 0,
            "day_of_week" => ParseDate(arguments).DayOfWeek.ToString(),
            "day_of_year" => (double)ParseDate(arguments).DayOfYear,
            "week_of_year" => (double)ISOWeek.GetWeekOfYear(ParseDate(arguments).ToDateTime(TimeOnly.MinValue)),
            "month_of_year" => (double)ParseDate(arguments).Month,
            "year" => (double)ParseDate(arguments).Year,
            "is" => Equals(arguments.ElementAtOrDefault(0), arguments.ElementAtOrDefault(1)),
            "not" => !AsBoolean(arguments.ElementAtOrDefault(0)),
            "get_entries" => GetEntries(arguments),
            "get_value" => GetValue(arguments),
            _ => null
        };

        return result is not null;
    }

    private static string Normalize(string functionName)
    {
        return functionName.Trim().ToLowerInvariant().Replace(' ', '_');
    }

    private static object ListContains(IReadOnlyList<object?> args)
    {
        List<object?> list = AsObjectList(args.ElementAtOrDefault(0));
        object? match = args.ElementAtOrDefault(1);
        return list.Any(x => Equals(x, match));
    }

    private static object Count(IReadOnlyList<object?> args) => (double)AsObjectList(args.ElementAtOrDefault(0)).Count;
    private static object Min(IReadOnlyList<object?> args) => AsObjectList(args.ElementAtOrDefault(0)).Select(AsDouble).DefaultIfEmpty(0d).Min();
    private static object Max(IReadOnlyList<object?> args) => AsObjectList(args.ElementAtOrDefault(0)).Select(AsDouble).DefaultIfEmpty(0d).Max();
    private static object Sum(IReadOnlyList<object?> args) => AsObjectList(args.ElementAtOrDefault(0)).Sum(AsDouble);
    private static object Mean(IReadOnlyList<object?> args)
    {
        double[] values = [.. AsObjectList(args.ElementAtOrDefault(0)).Select(AsDouble)];
        return values.Length == 0 ? 0d : values.Average();
    }

    private static object All(IReadOnlyList<object?> args) => AsObjectList(args.ElementAtOrDefault(0)).All(AsBoolean);
    private static object Any(IReadOnlyList<object?> args) => AsObjectList(args.ElementAtOrDefault(0)).Any(AsBoolean);

    private static object Sublist(IReadOnlyList<object?> args)
    {
        List<object?> list = AsObjectList(args.ElementAtOrDefault(0));
        int start = Math.Max(1, (int)AsDouble(args, 1));
        int? length = args.Count > 2 ? (int?)Math.Max(0, (int)AsDouble(args, 2)) : null;
        IEnumerable<object?> query = list.Skip(start - 1);
        if (length.HasValue)
        {
            query = query.Take(length.Value);
        }

        return query.ToArray();
    }

    private static object Append(IReadOnlyList<object?> args)
    {
        List<object?> list = AsObjectList(args.ElementAtOrDefault(0));
        list.Add(args.ElementAtOrDefault(1));
        return list.ToArray();
    }

    private static object Concat(IReadOnlyList<object?> args)
    {
        List<object?> left = AsObjectList(args.ElementAtOrDefault(0));
        List<object?> right = AsObjectList(args.ElementAtOrDefault(1));
        return left.Concat(right).ToArray();
    }

    private static object InsertBefore(IReadOnlyList<object?> args)
    {
        List<object?> list = AsObjectList(args.ElementAtOrDefault(0));
        int position = Math.Max(1, (int)AsDouble(args, 1));
        int index = Math.Min(list.Count, position - 1);
        list.Insert(index, args.ElementAtOrDefault(2));
        return list.ToArray();
    }

    private static object Remove(IReadOnlyList<object?> args)
    {
        List<object?> list = AsObjectList(args.ElementAtOrDefault(0));
        int position = Math.Max(1, (int)AsDouble(args, 1));
        if (position - 1 < list.Count)
        {
            list.RemoveAt(position - 1);
        }

        return list.ToArray();
    }

    private static object Reverse(IReadOnlyList<object?> args) => AsObjectList(args.ElementAtOrDefault(0)).AsEnumerable().Reverse().ToArray();

    private static object IndexOf(IReadOnlyList<object?> args)
    {
        List<object?> list = AsObjectList(args.ElementAtOrDefault(0));
        object? match = args.ElementAtOrDefault(1);
        List<double> indexes = [];
        for (int i = 0; i < list.Count; i++)
        {
            if (Equals(list[i], match))
            {
                indexes.Add(i + 1);
            }
        }

        return indexes.ToArray();
    }

    private static object Union(IReadOnlyList<object?> args)
    {
        List<object?> left = AsObjectList(args.ElementAtOrDefault(0));
        List<object?> right = AsObjectList(args.ElementAtOrDefault(1));
        return left.Concat(right).Distinct().ToArray();
    }

    private static object DistinctValues(IReadOnlyList<object?> args) => AsObjectList(args.ElementAtOrDefault(0)).Distinct().ToArray();

    private static object Flatten(IReadOnlyList<object?> args)
    {
        List<object?> flat = [];
        FlattenInternal(args.ElementAtOrDefault(0), flat);
        return flat.ToArray();
    }

    private static void FlattenInternal(object? value, List<object?> flat)
    {
        if (value is null)
        {
            return;
        }

        if (value is string)
        {
            flat.Add(value);
            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                FlattenInternal(item, flat);
            }

            return;
        }

        flat.Add(value);
    }

    private static object Sort(IReadOnlyList<object?> args)
    {
        List<object?> list = AsObjectList(args.ElementAtOrDefault(0));
        return list.OrderBy(x => x).ToArray();
    }

    private static object Substring(IReadOnlyList<object?> args)
    {
        string input = AsString(args.ElementAtOrDefault(0));
        int start = Math.Max(1, (int)AsDouble(args, 1));
        int? length = args.Count > 2 ? (int?)Math.Max(0, (int)AsDouble(args, 2)) : null;
        if (start > input.Length)
        {
            return string.Empty;
        }

        return length.HasValue
            ? input.Substring(start - 1, Math.Min(length.Value, input.Length - (start - 1)))
            : input[(start - 1)..];
    }

    private static object SubstringBefore(IReadOnlyList<object?> args)
    {
        string text = AsString(args.ElementAtOrDefault(0));
        string match = AsString(args.ElementAtOrDefault(1));
        int index = text.IndexOf(match, StringComparison.Ordinal);
        return index >= 0 ? text[..index] : string.Empty;
    }

    private static object SubstringAfter(IReadOnlyList<object?> args)
    {
        string text = AsString(args.ElementAtOrDefault(0));
        string match = AsString(args.ElementAtOrDefault(1));
        int index = text.IndexOf(match, StringComparison.Ordinal);
        return index >= 0 ? text[(index + match.Length)..] : string.Empty;
    }

    private static object Replace(IReadOnlyList<object?> args)
    {
        string input = AsString(args.ElementAtOrDefault(0));
        string pattern = AsString(args.ElementAtOrDefault(1));
        string replacement = AsString(args.ElementAtOrDefault(2));
        string flags = args.Count > 3 ? AsString(args.ElementAtOrDefault(3)) : string.Empty;
        RegexOptions options = flags.Contains('i', StringComparison.OrdinalIgnoreCase)
            ? RegexOptions.IgnoreCase
            : RegexOptions.None;
        return Regex.Replace(input, pattern, replacement, options);
    }

    private static object Contains(IReadOnlyList<object?> args)
    {
        string input = AsString(args.ElementAtOrDefault(0));
        string match = AsString(args.ElementAtOrDefault(1));
        return input.Contains(match, StringComparison.OrdinalIgnoreCase);
    }

    private static object StartsWith(IReadOnlyList<object?> args)
    {
        string input = AsString(args.ElementAtOrDefault(0));
        string match = AsString(args.ElementAtOrDefault(1));
        return input.StartsWith(match, StringComparison.OrdinalIgnoreCase);
    }

    private static object EndsWith(IReadOnlyList<object?> args)
    {
        string input = AsString(args.ElementAtOrDefault(0));
        string match = AsString(args.ElementAtOrDefault(1));
        return input.EndsWith(match, StringComparison.OrdinalIgnoreCase);
    }

    private static object Matches(IReadOnlyList<object?> args)
    {
        string input = AsString(args.ElementAtOrDefault(0));
        string pattern = AsString(args.ElementAtOrDefault(1));
        string flags = args.Count > 2 ? AsString(args.ElementAtOrDefault(2)) : string.Empty;
        RegexOptions options = flags.Contains('i', StringComparison.OrdinalIgnoreCase)
            ? RegexOptions.IgnoreCase
            : RegexOptions.None;
        return Regex.IsMatch(input, pattern, options);
    }

    private static object Split(IReadOnlyList<object?> args)
    {
        string input = AsString(args.ElementAtOrDefault(0));
        string delimiter = AsString(args.ElementAtOrDefault(1));
        return input.Split(delimiter, StringSplitOptions.None);
    }

    private static object Decimal(IReadOnlyList<object?> args)
    {
        double number = AsDouble(args, 0);
        int scale = (int)AsDouble(args, 1);
        return Math.Round(number, scale);
    }

    private static object GetEntries(IReadOnlyList<object?> args)
    {
        if (args.ElementAtOrDefault(0) is IDictionary<string, object?> mapNullable)
        {
            return mapNullable.Select(kv =>
            {
                Dictionary<string, object?> objects = new()
                {
                    ["key"] = kv.Key,
                    ["value"] = kv.Value
                };
                return objects;
            }).ToArray();
        }

        if (args.ElementAtOrDefault(0) is IDictionary<string, object> map)
        {
            return map.Select(kv =>
            {
                Dictionary<string, object?> objects = new()
                {
                    ["key"] = kv.Key,
                    ["value"] = kv.Value
                };
                return objects;
            }).ToArray();
        }

        return Array.Empty<object>();
    }

    private static object? GetValue(IReadOnlyList<object?> args)
    {
        string key = AsString(args.ElementAtOrDefault(1));
        if (args.ElementAtOrDefault(0) is IDictionary<string, object?> mapNullable && mapNullable.TryGetValue(key, out object? valueNullable))
        {
            return valueNullable;
        }

        if (args.ElementAtOrDefault(0) is IDictionary<string, object> map && map.TryGetValue(key, out object? value))
        {
            return value;
        }

        return null;
    }

    private static DateOnly ParseDate(IReadOnlyList<object?> args)
    {
        if (args.Count >= 3)
        {
            return new DateOnly((int)AsDouble(args, 0), (int)AsDouble(args, 1), (int)AsDouble(args, 2));
        }

        string text = AsString(args.ElementAtOrDefault(0));
        return DateOnly.Parse(text, CultureInfo.InvariantCulture);
    }

    private static TimeOnly ParseTime(IReadOnlyList<object?> args)
    {
        if (args.Count >= 3)
        {
            return new TimeOnly((int)AsDouble(args, 0), (int)AsDouble(args, 1), (int)AsDouble(args, 2));
        }

        string text = AsString(args.ElementAtOrDefault(0));
        return TimeOnly.Parse(text, CultureInfo.InvariantCulture);
    }

    private static DateTime ParseDateTime(IReadOnlyList<object?> args)
    {
        if (args.Count >= 2)
        {
            DateOnly date = ParseDate([args[0]]);
            TimeOnly time = ParseTime([args[1]]);
            return date.ToDateTime(time, DateTimeKind.Utc);
        }

        return DateTime.Parse(AsString(args.ElementAtOrDefault(0)), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    }

    private static object ParseDuration(IReadOnlyList<object?> args)
    {
        string raw = AsString(args.ElementAtOrDefault(0));
        if (XmlConvert.ToTimeSpan(raw) is { } duration)
        {
            return duration;
        }

        return TimeSpan.Zero;
    }

    private static object YearsAndMonthsDuration(IReadOnlyList<object?> args)
    {
        DateOnly start = ParseDate([args.ElementAtOrDefault(0)]);
        DateOnly end = ParseDate([args.ElementAtOrDefault(1)]);
        int months = (end.Year - start.Year) * 12 + (end.Month - start.Month);
        return months;
    }

    private static List<object?> AsObjectList(object? value)
    {
        if (value is null)
        {
            return [];
        }

        if (value is string)
        {
            return [value];
        }

        if (value is IEnumerable<object?> generic)
        {
            return [.. generic];
        }

        if (value is IEnumerable enumerable)
        {
            List<object?> list = [];
            foreach (object? item in enumerable)
            {
                list.Add(item);
            }

            return list;
        }

        return [value];
    }

    private static double AsDouble(object? value)
    {
        return value switch
        {
            null => 0d,
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            long l => l,
            _ => double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
                ? result
                : 0d
        };
    }

    private static double AsDouble(IReadOnlyList<object?> args, int index)
    {
        return AsDouble(args.ElementAtOrDefault(index));
    }

    private static bool AsBoolean(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            string s when bool.TryParse(s, out bool parsed) => parsed,
            string s => !string.IsNullOrWhiteSpace(s),
            _ => Math.Abs(AsDouble(value)) > 1e-9
        };
    }

    private static string AsString(object? value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
