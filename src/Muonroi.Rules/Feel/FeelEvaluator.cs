using System.Globalization;
using System.Text;

namespace Muonroi.Rules.Feel;

/// <summary>
/// Provides methods to evaluate FEEL (Friendly Enough Expression Language) expressions.
/// </summary>
public static partial class FeelEvaluator
{
    private const double NumericTolerance = 1e-9;

    [GeneratedRegex(@"^(?<var>\w+)\s+in\s+\[(?<min>-?\d+(?:\.\d+)?)\.\.(?<max>-?\d+(?:\.\d+)?)\]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"^(?<var>\w+)\s*(?<op>>=|<=|>|<|=)\s*(?<val>-?\d+(?:\.\d+)?)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ComparisonRegex();

    [GeneratedRegex(@"^(?<var>\w+)\s+in\s*\((?<vals>[^\)]+)\)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InListRegex();

    [GeneratedRegex(@"^(?<var>\w+)\s+matches\s+""(?<rx>.+)""$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MatchesRegex();

    /// <summary>
    /// Evaluates a FEEL expression and returns a boolean result.
    /// </summary>
    /// <param name="expression">The FEEL expression to evaluate.</param>
    /// <param name="variables">The context variables for the expression.</param>
    /// <returns>True if the expression evaluates to true; otherwise, false.</returns>
    public static bool Evaluate(string expression, IDictionary<string, object> variables)
    {
        return ToBoolean(EvaluateValue(expression, variables));
    }

    /// <summary>
    /// Evaluates a FEEL expression and returns the resulting value.
    /// </summary>
    /// <param name="expression">The FEEL expression to evaluate.</param>
    /// <param name="variables">The context variables for the expression.</param>
    /// <returns>The result of the evaluation, or null if evaluation fails.</returns>
    public static object? EvaluateValue(string expression, IDictionary<string, object> variables)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        expression = PreprocessExpression(expression.Trim().Replace("\r", string.Empty, StringComparison.Ordinal));

        if (TryEvaluateIfThenElse(expression, variables, out object? conditional))
        {
            return conditional;
        }

        if (TryEvaluateForExpression(expression, variables, out object? forResult))
        {
            return forResult;
        }

        if (TryEvaluateQuantified(expression, variables, out object? quantifiedResult))
        {
            return quantifiedResult;
        }

        if (TryEvaluateFilterExpression(expression, variables, out object? filterResult))
        {
            return filterResult;
        }

        if (TryEvaluateContextProjection(expression, variables, out object? contextProjection))
        {
            return contextProjection;
        }

        if (TryEvaluateBetweenExpression(expression, variables, out object? betweenResult))
        {
            return betweenResult;
        }

        if (TryEvaluateInstanceOfExpression(expression, variables, out object? instanceResult))
        {
            return instanceResult;
        }

        if (TryEvaluateLegacyRange(expression, variables, out bool rangeResult))
        {
            return rangeResult;
        }

        if (TryEvaluateLegacyComparison(expression, variables, out bool comparisonResult))
        {
            return comparisonResult;
        }

        if (TryEvaluateLegacyInList(expression, variables, out bool inListResult))
        {
            return inListResult;
        }

        Match matches = MatchesRegex().Match(expression);
        if (matches.Success)
        {
            string key = matches.Groups["var"].Value;
            string val = variables.TryGetValue(key, out object? obj) ? obj?.ToString() ?? string.Empty : string.Empty;
            string rx = matches.Groups["rx"].Value;
            return Regex.IsMatch(val, rx, RegexOptions.CultureInvariant);
        }

        try
        {
            Parser parser = new(expression, variables);
            object? result = parser.ParseExpression();
            parser.EnsureEnd();
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static string PreprocessExpression(string expression)
    {
        Dictionary<string, string> aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["string length"] = "string_length",
            ["substring before"] = "substring_before",
            ["substring after"] = "substring_after",
            ["upper case"] = "upper_case",
            ["lower case"] = "lower_case",
            ["starts with"] = "starts_with",
            ["ends with"] = "ends_with",
            ["list contains"] = "list_contains",
            ["distinct values"] = "distinct_values",
            ["insert before"] = "insert_before",
            ["index of"] = "index_of",
            ["day of week"] = "day_of_week",
            ["day of year"] = "day_of_year",
            ["week of year"] = "week_of_year",
            ["month of year"] = "month_of_year",
            ["date and time"] = "date_and_time",
            ["years and months duration"] = "years_and_months_duration",
            ["days and time duration"] = "days_and_time_duration",
            ["get entries"] = "get_entries",
            ["get value"] = "get_value"
        };

        foreach ((string source, string target) in aliases)
        {
            expression = Regex.Replace(
                expression,
                $@"\b{Regex.Escape(source)}\s*\(",
                $"{target}(",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return expression;
    }

    private static bool TryEvaluateIfThenElse(string expression, IDictionary<string, object> variables, out object? result)
    {
        result = null;
        Match match = Regex.Match(expression,
            @"^if\s+(?<cond>.+?)\s+then\s+(?<yes>.+?)\s+else\s+(?<no>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        object? condition = EvaluateValue(match.Groups["cond"].Value, variables);
        result = ToBoolean(condition)
            ? EvaluateValue(match.Groups["yes"].Value, variables)
            : EvaluateValue(match.Groups["no"].Value, variables);
        return true;
    }

    private static bool TryEvaluateForExpression(string expression, IDictionary<string, object> variables, out object? result)
    {
        result = null;
        Match match = Regex.Match(expression,
            @"^for\s+(?<var>[A-Za-z_]\w*)\s+in\s+(?<src>.+?)\s+return\s+(?<body>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        string sourceRaw = match.Groups["src"].Value.Trim();
        string body = match.Groups["body"].Value.Trim();
        string varName = match.Groups["var"].Value;

        List<object?> sequence = [];
        Match rangeMatch = Regex.Match(sourceRaw, @"^(?<start>-?\d+)\s*\.\.\s*(?<end>-?\d+)$",
            RegexOptions.CultureInvariant);
        if (rangeMatch.Success)
        {
            int start = int.Parse(rangeMatch.Groups["start"].Value, CultureInfo.InvariantCulture);
            int end = int.Parse(rangeMatch.Groups["end"].Value, CultureInfo.InvariantCulture);
            if (start <= end)
            {
                for (int i = start; i <= end; i++)
                {
                    sequence.Add((double)i);
                }
            }
            else
            {
                for (int i = start; i >= end; i--)
                {
                    sequence.Add((double)i);
                }
            }
        }
        else
        {
            object? srcValue = EvaluateValue(sourceRaw, variables);
            if (!TryAsObjectList(srcValue, out sequence))
            {
                return false;
            }
        }

        List<object?> output = [];
        foreach (object? item in sequence)
        {
            Dictionary<string, object> scoped = new(variables, StringComparer.OrdinalIgnoreCase)
            {
                [varName] = item!
            };
            output.Add(EvaluateValue(body, scoped));
        }

        result = output.ToArray();
        return true;
    }

    private static bool TryEvaluateQuantified(string expression, IDictionary<string, object> variables, out object? result)
    {
        result = null;
        Match match = Regex.Match(expression,
            @"^(?<q>some|every)\s+(?<var>[A-Za-z_]\w*)\s+in\s+(?<src>.+?)\s+satisfies\s+(?<pred>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        string quantifier = match.Groups["q"].Value.ToLowerInvariant();
        string varName = match.Groups["var"].Value;
        string sourceRaw = match.Groups["src"].Value.Trim();
        string predicate = match.Groups["pred"].Value.Trim();

        object? sourceValue = EvaluateValue(sourceRaw, variables);
        if (!TryAsObjectList(sourceValue, out List<object?>? items))
        {
            return false;
        }

        bool eval = quantifier == "some"
            ? items.Any(item => EvaluatePredicate(predicate, variables, varName, item))
            : items.All(item => EvaluatePredicate(predicate, variables, varName, item));

        result = eval;
        return true;
    }

    private static bool TryEvaluateFilterExpression(string expression, IDictionary<string, object> variables, out object? result)
    {
        result = null;
        Match match = Regex.Match(expression,
            @"^(?<src>\[[^\]]*\]|[A-Za-z_]\w*)\[(?<pred>.+)\]$",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        string sourceRaw = match.Groups["src"].Value.Trim();
        string predicate = match.Groups["pred"].Value.Trim();
        if (int.TryParse(predicate, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        object? sourceValue = EvaluateValue(sourceRaw, variables);
        if (!TryAsObjectList(sourceValue, out List<object?>? items))
        {
            return false;
        }

        object?[] filtered = [.. items.Where(item => EvaluatePredicate(predicate, variables, "item", item))];

        result = filtered;
        return true;
    }

    private static bool TryEvaluateContextProjection(string expression, IDictionary<string, object> variables, out object? result)
    {
        result = null;
        Match match = Regex.Match(expression,
            @"^(?<ctx>\{.+\})\.(?<member>[A-Za-z_]\w*)$",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        object? contextValue = EvaluateValue(match.Groups["ctx"].Value, variables);
        if (contextValue is IDictionary<string, object?> nullableDict &&
            nullableDict.TryGetValue(match.Groups["member"].Value, out object? nullableValue))
        {
            result = nullableValue;
            return true;
        }

        if (contextValue is IDictionary<string, object> dict &&
            dict.TryGetValue(match.Groups["member"].Value, out object? value))
        {
            result = value;
            return true;
        }

        result = GetMemberValue(contextValue!, match.Groups["member"].Value);
        return true;
    }

    private static bool TryEvaluateBetweenExpression(string expression, IDictionary<string, object> variables, out object? result)
    {
        result = null;
        Match match = Regex.Match(expression,
            @"^(?<val>.+)\s+between\s+(?<min>.+)\s+and\s+(?<max>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        object? value = EvaluateValue(match.Groups["val"].Value.Trim(), variables);
        object? min = EvaluateValue(match.Groups["min"].Value.Trim(), variables);
        object? max = EvaluateValue(match.Groups["max"].Value.Trim(), variables);
        result = CompareValues(value, min) >= 0 && CompareValues(value, max) <= 0;
        return true;
    }

    private static bool TryEvaluateInstanceOfExpression(string expression, IDictionary<string, object> variables, out object? result)
    {
        result = null;
        Match match = Regex.Match(expression,
            @"^(?<val>.+)\s+instance\s+of\s+(?<type>[A-Za-z_]\w*)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        object? value = EvaluateValue(match.Groups["val"].Value.Trim(), variables);
        string type = match.Groups["type"].Value.Trim().ToLowerInvariant();
        result = type switch
        {
            "number" => value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal,
            "string" => value is string,
            "boolean" => value is bool,
            "list" => value is IEnumerable<object?> || value is System.Collections.IEnumerable and not string,
            "context" => value is IDictionary<string, object> || value is IDictionary<string, object?>,
            "date" => value is DateOnly or DateTime,
            "time" => value is TimeOnly or TimeSpan,
            _ => false
        };
        return true;
    }

    private static bool EvaluatePredicate(string predicate, IDictionary<string, object> variables, string variableName, object? value)
    {
        Dictionary<string, object> scoped = new(variables, StringComparer.OrdinalIgnoreCase)
        {
            [variableName] = value!
        };
        return ToBoolean(EvaluateValue(predicate, scoped));
    }

    private static bool TryAsObjectList(object? source, out List<object?> values)
    {
        values = [];
        if (source is null or string)
        {
            return false;
        }

        if (source is IEnumerable<object?> generic)
        {
            values.AddRange(generic);
            return true;
        }

        if (source is System.Collections.IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                values.Add(item);
            }

            return true;
        }

        return false;
    }

    private static bool TryEvaluateLegacyRange(string expression, IDictionary<string, object> variables, out bool result)
    {
        result = false;
        Match range = RangeRegex().Match(expression);
        if (!range.Success)
        {
            return false;
        }

        if (!TryGetDouble(variables, range.Groups["var"].Value, out double value))
        {
            return false;
        }

        if (!double.TryParse(range.Groups["min"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double min))
        {
            return false;
        }

        if (!double.TryParse(range.Groups["max"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double max))
        {
            return false;
        }

        result = value >= min && value <= max;
        return true;
    }

    private static bool TryEvaluateLegacyComparison(string expression, IDictionary<string, object> variables, out bool result)
    {
        result = false;
        Match cmp = ComparisonRegex().Match(expression);
        if (!cmp.Success)
        {
            return false;
        }

        if (!TryGetDouble(variables, cmp.Groups["var"].Value, out double value))
        {
            return false;
        }

        if (!double.TryParse(cmp.Groups["val"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
        {
            return false;
        }

        result = cmp.Groups["op"].Value switch
        {
            ">" => value > val,
            ">=" => value >= val,
            "<" => value < val,
            "<=" => value <= val,
            "=" => Math.Abs(value - val) <= NumericTolerance,
            _ => false
        };
        return true;
    }

    private static bool TryEvaluateLegacyInList(string expression, IDictionary<string, object> variables, out bool result)
    {
        result = false;
        Match inList = InListRegex().Match(expression);
        if (!inList.Success)
        {
            return false;
        }

        string key = inList.Groups["var"].Value;
        string? strVal = variables.TryGetValue(key, out object? obj) ? obj?.ToString() : null;
        if (strVal is null)
        {
            return false;
        }

        string[] options = inList.Groups["vals"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        result = options.Contains(strVal, StringComparer.OrdinalIgnoreCase);
        return true;
    }

    private static bool TryGetDouble(IDictionary<string, object> vars, string key, out double value)
    {
        value = default;
        if (!vars.TryGetValue(key, out object? obj) || obj is null) return false;

        if (obj is double d)
        {
            value = d;
            return true;
        }

        string? s = Convert.ToString(obj, CultureInfo.InvariantCulture);
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool ToBoolean(object? value)
    {
        return value switch
        {
            bool b => b,
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            sbyte sb => sb != 0,
            byte b => b != 0,
            short s => s != 0,
            ushort us => us != 0,
            int i => i != 0,
            uint ui => ui != 0,
            long l => l != 0L,
            ulong ul => ul != 0UL,
            float f => Math.Abs(f) > NumericTolerance,
            double d => Math.Abs(d) > NumericTolerance,
            decimal m => Math.Abs(m) > 0m,
            _ => true
        };
    }

    private static int CompareValues(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (left is DateTime ldt && right is DateTime rdt)
        {
            return ldt.CompareTo(rdt);
        }

        if (left is DateOnly ld && right is DateOnly rd)
        {
            return ld.CompareTo(rd);
        }

        if (double.TryParse(Convert.ToString(left, CultureInfo.InvariantCulture), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double lnum) &&
            double.TryParse(Convert.ToString(right, CultureInfo.InvariantCulture), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double rnum))
        {
            double diff = lnum - rnum;
            if (Math.Abs(diff) <= NumericTolerance)
            {
                return 0;
            }

            return diff < 0 ? -1 : 1;
        }

        return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static double AsDouble(object? value)
    {
        if (value is null)
        {
            return 0d;
        }

        return Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    private static object? ResolvePath(IDictionary<string, object> variables, string path)
    {
        List<PathSegment> segments = ParseSegments(path);
        if (segments.Count == 0)
        {
            return null;
        }

        return ResolveSegment(variables, segments, 0);
    }

    private static object? ResolveSegment(object? current, IReadOnlyList<PathSegment> segments, int index)
    {
        if (current is null)
        {
            return null;
        }

        if (index >= segments.Count)
        {
            return current;
        }

        PathSegment segment = segments[index];
        object? member = GetMemberValue(current, segment.Name);
        if (segment.Index is null)
        {
            return ResolveSegment(member, segments, index + 1);
        }

        if (!TryAsEnumerable(member, out List<object?>? elements))
        {
            return null;
        }

        if (segment.Index == "*")
        {
            List<object?> results = [];
            foreach (object? element in elements)
            {
                object? resolved = ResolveSegment(element, segments, index + 1);
                if (resolved is IEnumerable<object?> nested && resolved is not string)
                {
                    results.AddRange(nested);
                }
                else
                {
                    results.Add(resolved);
                }
            }

            return results;
        }

        if (!int.TryParse(segment.Index, NumberStyles.Integer, CultureInfo.InvariantCulture, out int elementIndex))
        {
            return null;
        }

        object? indexed = elements.Skip(elementIndex).FirstOrDefault();
        return ResolveSegment(indexed, segments, index + 1);
    }

    private static List<PathSegment> ParseSegments(string path)
    {
        MatchCollection matches = Regex.Matches(path, @"(?<name>[A-Za-z_]\w*)(?:\[(?<index>\d+|\*)\])?",
            RegexOptions.CultureInvariant);
        List<PathSegment> segments = [];
        foreach (Match match in matches)
        {
            string name = match.Groups["name"].Value;
            string? index = match.Groups["index"].Success ? match.Groups["index"].Value : null;
            segments.Add(new PathSegment(name, index));
        }

        return segments;
    }

    private static object? GetMemberValue(object source, string member)
    {
        if (source is IDictionary<string, object?> nullableDict && nullableDict.TryGetValue(member, out object? nullableValue))
        {
            return nullableValue;
        }

        if (source is IDictionary<string, object> dict && dict.TryGetValue(member, out object? value))
        {
            return value;
        }

        Type type = source.GetType();
        System.Reflection.PropertyInfo? property = type.GetProperty(member,
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.IgnoreCase);
        if (property is not null)
        {
            return property.GetValue(source);
        }

        System.Reflection.FieldInfo? field = type.GetField(member,
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.IgnoreCase);
        return field?.GetValue(source);
    }

    private static bool TryAsEnumerable(object? value, out List<object?> elements)
    {
        elements = [];
        if (value is null || value is string || value is JsonElement { ValueKind: JsonValueKind.String })
        {
            return false;
        }

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in jsonElement.EnumerateArray())
            {
                elements.Add(ConvertJson(item));
            }

            return true;
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                elements.Add(item);
            }

            return true;
        }

        return false;
    }

    private static object? ConvertJson(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDouble(out double number) => number,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJson).ToList(),
            _ => element.GetRawText()
        };
    }

    private sealed record PathSegment(string Name, string? Index);

    private sealed record RangeValue(object? Min, object? Max, bool IncludeMin = true, bool IncludeMax = true);

    private sealed record UserDefinedFunction(
        IReadOnlyList<string> Parameters,
        string Body,
        IReadOnlyDictionary<string, object> Closure);

    private sealed class Parser(string expression, IDictionary<string, object> variables)
    {
        private readonly IReadOnlyList<Token> _tokens = Tokenize(expression);
        private readonly Dictionary<string, object> _scope = new(variables, StringComparer.OrdinalIgnoreCase);
        private int _position;

        public object? ParseExpression()
        {
            return ParseOr();
        }

        public void EnsureEnd()
        {
            if (Current.Kind != TokenKind.End)
            {
                throw new InvalidOperationException("Unexpected token at end of expression.");
            }
        }

        private object? ParseOr()
        {
            object? left = ParseAnd();
            while (Match(TokenKind.Or))
            {
                object? right = ParseAnd();
                left = ToBoolean(left) || ToBoolean(right);
            }

            return left;
        }

        private object? ParseAnd()
        {
            object? left = ParseNot();
            while (Match(TokenKind.And))
            {
                object? right = ParseNot();
                left = ToBoolean(left) && ToBoolean(right);
            }

            return left;
        }

        private object? ParseNot()
        {
            if (Match(TokenKind.Not))
            {
                return !ToBoolean(ParseNot());
            }

            return ParseComparison();
        }

        private object? ParseComparison()
        {
            object? left = ParseAdditive();
            if (Match(TokenKind.In))
            {
                object? right = ParseInOperand();
                return EvaluateIn(left, right);
            }

            if (Match(TokenKind.Contains))
            {
                object? right = ParseAdditive();
                return (left?.ToString() ?? string.Empty).Contains(right?.ToString() ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (Match(TokenKind.StartsWith))
            {
                object? right = ParseAdditive();
                return (left?.ToString() ?? string.Empty).StartsWith(right?.ToString() ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (Match(TokenKind.Equal))
            {
                object? right = ParseAdditive();
                return Compare(left, right) == 0;
            }

            if (Match(TokenKind.NotEqual))
            {
                object? right = ParseAdditive();
                return Compare(left, right) != 0;
            }

            if (Match(TokenKind.Greater))
            {
                object? right = ParseAdditive();
                return Compare(left, right) > 0;
            }

            if (Match(TokenKind.GreaterOrEqual))
            {
                object? right = ParseAdditive();
                return Compare(left, right) >= 0;
            }

            if (Match(TokenKind.Less))
            {
                object? right = ParseAdditive();
                return Compare(left, right) < 0;
            }

            if (Match(TokenKind.LessOrEqual))
            {
                object? right = ParseAdditive();
                return Compare(left, right) <= 0;
            }

            return left;
        }

        private object? ParseInOperand()
        {
            if (Match(TokenKind.LeftBracket))
            {
                object? min = ParseAdditive();
                if (Match(TokenKind.DotDot))
                {
                    object? max = ParseAdditive();
                    if (Match(TokenKind.RightBracket))
                    {
                        return new RangeValue(min, max, true, true);
                    }

                    if (Match(TokenKind.RightParen))
                    {
                        return new RangeValue(min, max, true, false);
                    }

                    throw new InvalidOperationException("Expected range closing bracket '] or )'.");
                }

                List<object?> list = [min];
                while (Match(TokenKind.Comma))
                {
                    list.Add(ParseAdditive());
                }

                Expect(TokenKind.RightBracket);
                return list;
            }

            if (Match(TokenKind.LeftParen))
            {
                if (Match(TokenKind.RightParen))
                {
                    return Array.Empty<object?>();
                }

                object? min = ParseAdditive();
                if (Match(TokenKind.DotDot))
                {
                    object? max = ParseAdditive();
                    if (Match(TokenKind.RightParen))
                    {
                        return new RangeValue(min, max, false, false);
                    }

                    if (Match(TokenKind.RightBracket))
                    {
                        return new RangeValue(min, max, false, true);
                    }

                    throw new InvalidOperationException("Expected range closing bracket '] or )'.");
                }

                List<object?> list = [min];
                while (Match(TokenKind.Comma))
                {
                    list.Add(ParseAdditive());
                }

                Expect(TokenKind.RightParen);
                return list;
            }

            return ParseAdditive();
        }

        private object? ParseAdditive()
        {
            object? left = ParseMultiplicative();
            while (Current.Kind is TokenKind.Plus or TokenKind.Minus)
            {
                TokenKind op = Current.Kind;
                Next();
                object? right = ParseMultiplicative();
                left = ApplyArithmetic(left, right, op);
            }

            return left;
        }

        private object? ParseMultiplicative()
        {
            object? left = ParseUnary();
            while (Current.Kind is TokenKind.Star or TokenKind.Slash)
            {
                TokenKind op = Current.Kind;
                Next();
                object? right = ParseUnary();
                left = ApplyArithmetic(left, right, op);
            }

            return left;
        }

        private object? ParseUnary()
        {
            if (Match(TokenKind.Minus))
            {
                return -AsDouble(ParseUnary());
            }

            return ParsePrimary();
        }

        private object? ParsePrimary()
        {
            if (Match(TokenKind.LeftParen))
            {
                object? nested = ParseExpression();
                Expect(TokenKind.RightParen);
                return nested;
            }

            if (Match(TokenKind.LeftBracket))
            {
                return ParseListLiteral();
            }

            if (Match(TokenKind.LeftBrace))
            {
                return ParseContextLiteral();
            }

            if (Current.Kind == TokenKind.Number)
            {
                string raw = Current.Text;
                Next();
                return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                    ? value
                    : 0d;
            }

            if (Current.Kind == TokenKind.String)
            {
                string text = Current.Text;
                Next();
                return text;
            }

            if (Current.Kind == TokenKind.True)
            {
                Next();
                return true;
            }

            if (Current.Kind == TokenKind.False)
            {
                Next();
                return false;
            }

            if (Current.Kind == TokenKind.Identifier)
            {
                if (Current.Text.Equals("for", StringComparison.OrdinalIgnoreCase) &&
                    Peek(1).Kind == TokenKind.Identifier)
                {
                    return ParseForExpression();
                }

                if ((Current.Text.Equals("some", StringComparison.OrdinalIgnoreCase) ||
                     Current.Text.Equals("every", StringComparison.OrdinalIgnoreCase)) &&
                    Peek(1).Kind == TokenKind.Identifier)
                {
                    return ParseQuantifiedExpression();
                }

                if (Current.Text.Equals("function", StringComparison.OrdinalIgnoreCase) &&
                    Peek(1).Kind == TokenKind.LeftParen)
                {
                    return ParseFunctionDefinition();
                }

                string identifier = Current.Text;
                Next();

                if (Match(TokenKind.LeftParen))
                {
                    List<object?> args = ParseArguments();
                    object? scopedCallable = ResolvePath(_scope, identifier);
                    if (scopedCallable is UserDefinedFunction udf)
                    {
                        return InvokeUserDefinedFunction(udf, args);
                    }

                    return InvokeFunction(identifier, args);
                }

                object? resolved = ResolvePath(_scope, identifier);
                if (resolved is not null)
                {
                    return resolved;
                }

                string root = identifier.Split('.', '[', StringSplitOptions.RemoveEmptyEntries)[0];
                return _scope.ContainsKey(root) ? null : identifier;
            }

            throw new InvalidOperationException("Unexpected token.");
        }

        private object? ParseListLiteral()
        {
            if (Match(TokenKind.RightBracket))
            {
                return Array.Empty<object?>();
            }

            object? first = ParseExpression();
            if (Match(TokenKind.DotDot))
            {
                object? second = ParseExpression();
                Expect(TokenKind.RightBracket);

                int start = (int)AsDouble(first);
                int end = (int)AsDouble(second);
                int step = start <= end ? 1 : -1;
                List<object?> items = [];
                for (int i = start; step > 0 ? i <= end : i >= end; i += step)
                {
                    items.Add((double)i);
                }

                return items.ToArray();
            }

            List<object?> values = [first];
            while (Match(TokenKind.Comma))
            {
                values.Add(ParseExpression());
            }

            Expect(TokenKind.RightBracket);
            return values.ToArray();
        }

        private object? ParseContextLiteral()
        {
            Dictionary<string, object?> entries = new(StringComparer.OrdinalIgnoreCase);
            if (Match(TokenKind.RightBrace))
            {
                return entries;
            }

            do
            {
                string key;
                if (Current.Kind == TokenKind.Identifier || Current.Kind == TokenKind.String)
                {
                    key = Current.Text;
                    Next();
                }
                else
                {
                    throw new InvalidOperationException("Expected context key.");
                }

                Expect(TokenKind.Colon);
                object? value = ParseExpression();
                entries[key] = value;
                _scope[key] = value!;
            } while (Match(TokenKind.Comma));

            Expect(TokenKind.RightBrace);
            return entries;
        }

        private List<object?> ParseArguments()
        {
            List<object?> args = [];
            if (Match(TokenKind.RightParen))
            {
                return args;
            }

            do
            {
                args.Add(ParseExpression());
            } while (Match(TokenKind.Comma));

            Expect(TokenKind.RightParen);
            return args;
        }

        private static object? InvokeFunction(string name, IReadOnlyList<object?> args)
        {
            if (FeelStandardLibrary.TryCall(name, args, out object? libraryResult))
            {
                return libraryResult;
            }

            return name.ToLowerInvariant() switch
            {
                "round" => Math.Round(AsDouble(args.ElementAtOrDefault(0)), (int)AsDouble(args.ElementAtOrDefault(1))),
                _ => throw new InvalidOperationException($"Unsupported function '{name}'.")
            };
        }

        /// <summary>
        /// Parses: for &lt;var&gt; in &lt;source&gt; return &lt;body&gt;
        /// The "for" identifier is at Current when called.
        /// </summary>
        private object? ParseForExpression()
        {
            Next(); // consume "for"

            if (Current.Kind != TokenKind.Identifier)
            {
                throw new InvalidOperationException("Expected variable name after 'for'.");
            }

            string varName = Current.Text;
            Next(); // consume variable name

            Expect(TokenKind.In); // consume "in"

            // Parse the source expression — stop when we see a "return" identifier
            object? source = ParseForSource();

            // Expect "return" keyword (tokenized as Identifier)
            if (Current.Kind != TokenKind.Identifier ||
                !Current.Text.Equals("return", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Expected 'return' in for expression.");
            }

            Next(); // consume "return"

            // Parse the body expression — uses the remaining expression
            // which can include nested property access like d.vgm.wgt
            List<object?> sequence = [];
            if (source is null or string)
            {
                return Array.Empty<object?>();
            }

            if (source is IEnumerable<object?> generic)
            {
                sequence.AddRange(generic);
            }
            else if (source is System.Collections.IEnumerable enumerable)
            {
                foreach (object? item in enumerable)
                {
                    sequence.Add(item);
                }
            }
            else
            {
                return Array.Empty<object?>();
            }

            // Save current position to re-parse body for each iteration
            int bodyStart = _position;
            List<object?> output = [];
            foreach (object? item in sequence)
            {
                _position = bodyStart;
                _scope[varName] = item!;
                output.Add(ParseExpression());
            }

            // Remove loop variable from scope after iteration
            _scope.Remove(varName);
            return output.ToArray();
        }

        /// <summary>
        /// Parses the source expression in a for-loop, stopping before the "return" keyword.
        /// </summary>
        private object? ParseForSource()
        {
            // If the source is a simple identifier (possibly with dots), parse it directly
            // We need to stop before "return" which is tokenized as Identifier
            // Use a simple approach: parse an additive expression, but peek ahead for "return"
            if (Current.Kind == TokenKind.Identifier &&
                Peek(1).Kind == TokenKind.Identifier &&
                Peek(1).Text.Equals("return", StringComparison.OrdinalIgnoreCase))
            {
                // Simple case: source is a single identifier like "details"
                string identifier = Current.Text;
                Next();
                return ResolvePath(_scope, identifier);
            }

            if (Current.Kind == TokenKind.LeftBracket)
            {
                // List literal: for x in [1, 2, 3] return ...
                Next(); // consume '['
                object? list = ParseListLiteral();
                // After list, expect "return"
                return list;
            }

            // For range expressions like "1..10", parse as additive
            object? left = ParseAdditive();
            if (Match(TokenKind.DotDot))
            {
                object? right = ParseAdditive();
                int start = (int)AsDouble(left);
                int end = (int)AsDouble(right);
                int step = start <= end ? 1 : -1;
                List<object?> range = [];
                for (int i = start; step > 0 ? i <= end : i >= end; i += step)
                {
                    range.Add((double)i);
                }

                return range;
            }

            return left;
        }

        /// <summary>
        /// Parses: some|every &lt;var&gt; in &lt;source&gt; satisfies &lt;predicate&gt;
        /// </summary>
        private object? ParseQuantifiedExpression()
        {
            string quantifier = Current.Text.ToLowerInvariant();
            Next(); // consume "some" or "every"

            if (Current.Kind != TokenKind.Identifier)
            {
                throw new InvalidOperationException($"Expected variable name after '{quantifier}'.");
            }

            string varName = Current.Text;
            Next(); // consume variable name

            Expect(TokenKind.In); // consume "in"

            // Parse the source (stop before "satisfies")
            object? source = ParseQuantifiedSource();

            // Expect "satisfies" keyword
            if (Current.Kind != TokenKind.Identifier ||
                !Current.Text.Equals("satisfies", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Expected 'satisfies' in {quantifier} expression.");
            }

            Next(); // consume "satisfies"

            List<object?> sequence = [];
            if (source is IEnumerable<object?> generic)
            {
                sequence.AddRange(generic);
            }
            else if (source is System.Collections.IEnumerable enumerable and not string)
            {
                foreach (object? item in enumerable)
                {
                    sequence.Add(item);
                }
            }
            else
            {
                return false;
            }

            int predicateStart = _position;
            if (quantifier == "some")
            {
                foreach (object? item in sequence)
                {
                    _position = predicateStart;
                    _scope[varName] = item!;
                    if (ToBoolean(ParseExpression()))
                    {
                        _scope.Remove(varName);
                        return true;
                    }
                }

                _scope.Remove(varName);
                return false;
            }
            else // every
            {
                foreach (object? item in sequence)
                {
                    _position = predicateStart;
                    _scope[varName] = item!;
                    if (!ToBoolean(ParseExpression()))
                    {
                        _scope.Remove(varName);
                        return false;
                    }
                }

                _scope.Remove(varName);
                return true;
            }
        }

        /// <summary>
        /// Parses the source expression in a quantified expression, stopping before "satisfies".
        /// </summary>
        private object? ParseQuantifiedSource()
        {
            if (Current.Kind == TokenKind.Identifier &&
                Peek(1).Kind == TokenKind.Identifier &&
                Peek(1).Text.Equals("satisfies", StringComparison.OrdinalIgnoreCase))
            {
                string identifier = Current.Text;
                Next();
                return ResolvePath(_scope, identifier);
            }

            if (Current.Kind == TokenKind.LeftBracket)
            {
                Next();
                return ParseListLiteral();
            }

            return ParseAdditive();
        }

        private object? ParseFunctionDefinition()
        {
            Expect(TokenKind.Identifier); // function
            Expect(TokenKind.LeftParen);

            List<string> parameters = [];
            if (!Match(TokenKind.RightParen))
            {
                do
                {
                    if (Current.Kind != TokenKind.Identifier)
                    {
                        throw new InvalidOperationException("Expected function parameter name.");
                    }

                    parameters.Add(Current.Text);
                    Next();
                } while (Match(TokenKind.Comma));

                Expect(TokenKind.RightParen);
            }

            string body = ReadFunctionBody();
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new InvalidOperationException("Function body is required.");
            }

            Dictionary<string, object> closure = new(_scope, StringComparer.OrdinalIgnoreCase);
            return new UserDefinedFunction(parameters, body, closure);
        }

        private static object? InvokeUserDefinedFunction(UserDefinedFunction udf, IReadOnlyList<object?> args)
        {
            Dictionary<string, object> localScope = new(udf.Closure, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < udf.Parameters.Count; i++)
            {
                localScope[udf.Parameters[i]] = args.ElementAtOrDefault(i)!;
            }

            return EvaluateValue(udf.Body, localScope);
        }

        private string ReadFunctionBody()
        {
            int start = _position;
            int parenDepth = 0;
            int bracketDepth = 0;
            int braceDepth = 0;

            while (Current.Kind != TokenKind.End)
            {
                if (Current.Kind == TokenKind.Comma && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                {
                    break;
                }

                if ((Current.Kind == TokenKind.RightBrace || Current.Kind == TokenKind.RightParen) &&
                    parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                {
                    break;
                }

                switch (Current.Kind)
                {
                    case TokenKind.LeftParen:
                        parenDepth++;
                        break;
                    case TokenKind.RightParen:
                        parenDepth--;
                        break;
                    case TokenKind.LeftBracket:
                        bracketDepth++;
                        break;
                    case TokenKind.RightBracket:
                        bracketDepth--;
                        break;
                    case TokenKind.LeftBrace:
                        braceDepth++;
                        break;
                    case TokenKind.RightBrace:
                        braceDepth--;
                        break;
                }

                Next();
            }

            return RebuildExpression(start, _position);
        }

        private string RebuildExpression(int start, int end)
        {
            if (start >= end)
            {
                return string.Empty;
            }

            StringBuilder sb = new();
            TokenKind? prev = null;

            for (int i = start; i < end; i++)
            {
                Token token = _tokens[i];
                if (token.Kind == TokenKind.End)
                {
                    break;
                }

                string text = token.Kind == TokenKind.String ? $"'{token.Text}'" : token.Text;
                if (NeedsSpace(prev, token.Kind))
                {
                    sb.Append(' ');
                }

                sb.Append(text);
                prev = token.Kind;
            }

            return sb.ToString();
        }

        private static bool NeedsSpace(TokenKind? previous, TokenKind current)
        {
            if (!previous.HasValue)
            {
                return false;
            }

            if (current is TokenKind.RightParen or TokenKind.RightBracket or TokenKind.RightBrace or TokenKind.Comma)
            {
                return false;
            }

            if (previous.Value is TokenKind.LeftParen or TokenKind.LeftBracket or TokenKind.LeftBrace or TokenKind.Comma)
            {
                return false;
            }

            return true;
        }

        private static double Sum(object? value)
        {
            if (value is null)
            {
                return 0d;
            }

            if (value is IEnumerable<object?> list)
            {
                return list.Sum(AsDouble);
            }

            if (value is System.Collections.IEnumerable enumerable and not string)
            {
                double total = 0d;
                foreach (object? item in enumerable)
                {
                    total += AsDouble(item);
                }

                return total;
            }

            return AsDouble(value);
        }

        private static object? ApplyArithmetic(object? left, object? right, TokenKind op)
        {
            if (left is DateTime dt && right is TimeSpan span)
            {
                return op == TokenKind.Plus ? dt + span : dt - span;
            }

            if (left is TimeSpan leftSpan && right is TimeSpan rightSpan)
            {
                return op == TokenKind.Plus ? leftSpan + rightSpan : leftSpan - rightSpan;
            }

            double l = AsDouble(left);
            double r = AsDouble(right);
            return op switch
            {
                TokenKind.Plus => l + r,
                TokenKind.Minus => l - r,
                TokenKind.Star => l * r,
                TokenKind.Slash => Math.Abs(r) <= NumericTolerance ? 0d : l / r,
                _ => l
            };
        }

        private static int Compare(object? left, object? right)
        {
            if (left is null && right is null)
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            if (left is DateTime ldt && right is DateTime rdt)
            {
                return ldt.CompareTo(rdt);
            }

            if (left is bool lb && right is bool rb)
            {
                return lb.CompareTo(rb);
            }

            if (double.TryParse(Convert.ToString(left, CultureInfo.InvariantCulture), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double lnum) &&
                double.TryParse(Convert.ToString(right, CultureInfo.InvariantCulture), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double rnum))
            {
                double diff = lnum - rnum;
                if (Math.Abs(diff) <= NumericTolerance)
                {
                    return 0;
                }

                return diff < 0 ? -1 : 1;
            }

            return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool EvaluateIn(object? value, object? right)
        {
            return right switch
            {
                RangeValue range => IsWithinRange(value, range),
                IEnumerable<object?> list => list.Any(item => Compare(value, item) == 0),
                _ => Compare(value, right) == 0
            };
        }

        private static bool IsWithinRange(object? value, RangeValue range)
        {
            int min = Compare(value, range.Min);
            int max = Compare(value, range.Max);
            bool minPass = range.IncludeMin ? min >= 0 : min > 0;
            bool maxPass = range.IncludeMax ? max <= 0 : max < 0;
            return minPass && maxPass;
        }

        private Token Current => _tokens[_position];

        private Token Peek(int offset)
        {
            int index = Math.Min(_position + offset, _tokens.Count - 1);
            return _tokens[index];
        }

        private bool Match(TokenKind kind)
        {
            if (Current.Kind != kind)
            {
                return false;
            }

            _position++;
            return true;
        }

        private void Expect(TokenKind kind)
        {
            if (!Match(kind))
            {
                throw new InvalidOperationException($"Expected token '{kind}'.");
            }
        }

        private void Next()
        {
            if (_position < _tokens.Count - 1)
            {
                _position++;
            }
        }

        private static List<Token> Tokenize(string input)
        {
            List<Token> tokens = [];
            int i = 0;
            while (i < input.Length)
            {
                char c = input[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (c is '\'' or '"')
                {
                    char quote = c;
                    i++;
                    int start = i;
                    while (i < input.Length && input[i] != quote)
                    {
                        i++;
                    }

                    string str = input[start..Math.Min(i, input.Length)];
                    if (i < input.Length && input[i] == quote)
                    {
                        i++;
                    }

                    tokens.Add(new Token(TokenKind.String, str));
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && i + 1 < input.Length && char.IsDigit(input[i + 1])))
                {
                    int start = i;
                    bool seenDot = c == '.';
                    i++;
                    while (i < input.Length)
                    {
                        if (char.IsDigit(input[i]))
                        {
                            i++;
                            continue;
                        }

                        if (input[i] == '.')
                        {
                            if (i + 1 < input.Length && input[i + 1] == '.')
                            {
                                break;
                            }

                            if (seenDot)
                            {
                                break;
                            }

                            seenDot = true;
                            i++;
                            continue;
                        }

                        break;
                    }

                    if (i == start + 1 && input[start] == '.')
                    {
                        throw new InvalidOperationException("Invalid numeric token.");
                    }

                    if (i < input.Length && input[i] == '.')
                    {
                        // Keep range operator '..' as separate token.
                    }

                    tokens.Add(new Token(TokenKind.Number, input[start..i]));
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    i++;
                    while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] is '_' or '.' or '[' or ']' or '*'))
                    {
                        i++;
                    }

                    string raw = input[start..i];
                    string upper = raw.ToUpperInvariant();
                    TokenKind kind = upper switch
                    {
                        "AND" => TokenKind.And,
                        "OR" => TokenKind.Or,
                        "NOT" => TokenKind.Not,
                        "IN" => TokenKind.In,
                        "CONTAINS" => TokenKind.Contains,
                        "STARTSWITH" => TokenKind.StartsWith,
                        "TRUE" => TokenKind.True,
                        "FALSE" => TokenKind.False,
                        _ => TokenKind.Identifier
                    };

                    tokens.Add(new Token(kind, raw));
                    continue;
                }

                if (c == '.' && i + 1 < input.Length && input[i + 1] == '.')
                {
                    tokens.Add(new Token(TokenKind.DotDot, ".."));
                    i += 2;
                    continue;
                }

                if (c == '>' && i + 1 < input.Length && input[i + 1] == '=')
                {
                    tokens.Add(new Token(TokenKind.GreaterOrEqual, ">="));
                    i += 2;
                    continue;
                }

                if (c == '<' && i + 1 < input.Length && input[i + 1] == '=')
                {
                    tokens.Add(new Token(TokenKind.LessOrEqual, "<="));
                    i += 2;
                    continue;
                }

                if (c == '!' && i + 1 < input.Length && input[i + 1] == '=')
                {
                    tokens.Add(new Token(TokenKind.NotEqual, "!="));
                    i += 2;
                    continue;
                }

                tokens.Add(c switch
                {
                    '(' => new Token(TokenKind.LeftParen, "("),
                    ')' => new Token(TokenKind.RightParen, ")"),
                    '[' => new Token(TokenKind.LeftBracket, "["),
                    ']' => new Token(TokenKind.RightBracket, "]"),
                    '{' => new Token(TokenKind.LeftBrace, "{"),
                    '}' => new Token(TokenKind.RightBrace, "}"),
                    ',' => new Token(TokenKind.Comma, ","),
                    ':' => new Token(TokenKind.Colon, ":"),
                    '+' => new Token(TokenKind.Plus, "+"),
                    '-' => new Token(TokenKind.Minus, "-"),
                    '*' => new Token(TokenKind.Star, "*"),
                    '/' => new Token(TokenKind.Slash, "/"),
                    '=' => new Token(TokenKind.Equal, "="),
                    '>' => new Token(TokenKind.Greater, ">"),
                    '<' => new Token(TokenKind.Less, "<"),
                    _ => throw new InvalidOperationException($"Unsupported token '{c}'.")
                });
                i++;
            }

            tokens.Add(new Token(TokenKind.End, string.Empty));
            return tokens;
        }
    }

    private readonly record struct Token(TokenKind Kind, string Text);

    private enum TokenKind
    {
        Identifier,
        Number,
        String,
        True,
        False,
        LeftParen,
        RightParen,
        LeftBracket,
        RightBracket,
        LeftBrace,
        RightBrace,
        Comma,
        Colon,
        DotDot,
        Plus,
        Minus,
        Star,
        Slash,
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
        And,
        Or,
        Not,
        In,
        Contains,
        StartsWith,
        End
    }
}
