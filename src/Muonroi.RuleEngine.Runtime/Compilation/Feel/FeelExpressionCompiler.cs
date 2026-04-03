using Muonroi.RuleEngine.DecisionTable.Feel;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Muonroi.RuleEngine.Runtime.Compilation.Feel;

/// <summary>
/// Compiles FEEL boolean expressions into cached delegates for hot execution paths.
/// </summary>
public static class FeelExpressionCompiler
{
    private static readonly ConcurrentDictionary<string, Func<IDictionary<string, object>, bool>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Compiles a FEEL expression into a cached boolean delegate.
    /// </summary>
    /// <param name="feelExpression">The FEEL expression to compile.</param>
    /// <returns>A cached delegate that evaluates the expression.</returns>
    public static Func<IDictionary<string, object>, bool> Compile(string feelExpression)
    {
        string normalized = (feelExpression ?? string.Empty).Trim();
        return Cache.GetOrAdd(normalized, BuildDelegate);
    }

    internal static object? GetValue(IDictionary<string, object> variables, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (variables.TryGetValue(path, out object? direct))
        {
            return direct;
        }

        string[] segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        if (!variables.TryGetValue(segments[0], out object? current))
        {
            return null;
        }

        for (int i = 1; i < segments.Length; i++)
        {
            if (current is null)
            {
                return null;
            }

            current = ResolveMember(current, segments[i]);
        }

        return current;
    }

    internal static bool ToBoolean(object? value)
    {
        return value switch
        {
            null => false,
            bool boolValue => boolValue,
            string stringValue when bool.TryParse(stringValue, out bool parsed) => parsed,
            string stringValue => !string.IsNullOrWhiteSpace(stringValue),
            _ when TryConvertNumber(value, out double number) => Math.Abs(number) > double.Epsilon,
            _ => true
        };
    }

    internal static bool CompareValues(object? left, object? right, string operation)
    {
        int comparison = Compare(left, right);
        return operation switch
        {
            "=" => comparison == 0,
            "!=" => comparison != 0,
            "<" => comparison < 0,
            "<=" => comparison <= 0,
            ">" => comparison > 0,
            ">=" => comparison >= 0,
            _ => throw new NotSupportedException($"Unsupported comparison '{operation}'.")
        };
    }

    internal static bool Contains(object? left, object? right)
    {
        string haystack = Convert.ToString(left, CultureInfo.InvariantCulture) ?? string.Empty;
        string needle = Convert.ToString(right, CultureInfo.InvariantCulture) ?? string.Empty;
        if (haystack.Length == 0 || needle.Length == 0)
        {
            return false;
        }

        return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static bool InOperator(object? left, object? right)
    {
        return right switch
        {
            FeelRangeBounds range => IsWithinRange(left, range),
            IEnumerable<object?> list => list.Any(item => Compare(left, item) == 0),
            _ => Compare(left, right) == 0
        };
    }

    internal static FeelRangeBounds CreateRange(object? minimum, object? maximum, bool includeMinimum, bool includeMaximum)
    {
        return new FeelRangeBounds(minimum, maximum, includeMinimum, includeMaximum);
    }

    private static Func<IDictionary<string, object>, bool> BuildDelegate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return _ => false;
        }

        try
        {
            ParameterExpression variablesParameter = Expression.Parameter(typeof(IDictionary<string, object>), "vars");
            FeelSyntaxNode syntax = FeelExpressionParser.Parse(expression);
            Expression body = ExpressionTreeVisitor.VisitAsBoolean(syntax, variablesParameter);
            return Expression.Lambda<Func<IDictionary<string, object>, bool>>(body, variablesParameter).Compile();
        }
        catch
        {
            return variables => EvaluateWithFallback(expression, variables);
        }
    }

    private static bool EvaluateWithFallback(string expression, IDictionary<string, object> variables)
    {
        Dictionary<string, object> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, object value) in variables)
        {
            normalized[key] = value;
        }

        return FeelEvaluator.Evaluate(expression, normalized);
    }

    private static object? ResolveMember(object source, string memberName)
    {
        if (source is IDictionary<string, object> dictionary && dictionary.TryGetValue(memberName, out object? dictValue))
        {
            return dictValue;
        }

        if (source is IReadOnlyDictionary<string, object> readOnlyDictionary && readOnlyDictionary.TryGetValue(memberName, out object? roValue))
        {
            return roValue;
        }

        PropertyInfo? property = source.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        return property?.GetValue(source);
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

        if (TryConvertNumber(left, out double leftNumber) && TryConvertNumber(right, out double rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (left is bool leftBool && right is bool rightBool)
        {
            return leftBool.CompareTo(rightBool);
        }

        if (left is DateTime leftDateTime && right is DateTime rightDateTime)
        {
            return leftDateTime.CompareTo(rightDateTime);
        }

        if (left is DateOnly leftDate && right is DateOnly rightDate)
        {
            return leftDate.CompareTo(rightDate);
        }

        return string.Compare(
            Convert.ToString(left, CultureInfo.InvariantCulture),
            Convert.ToString(right, CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryConvertNumber(object? value, out double number)
    {
        return double.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static bool IsWithinRange(object? value, FeelRangeBounds range)
    {
        int minimum = Compare(value, range.Minimum);
        int maximum = Compare(value, range.Maximum);
        bool minimumPass = range.IncludeMinimum ? minimum >= 0 : minimum > 0;
        bool maximumPass = range.IncludeMaximum ? maximum <= 0 : maximum < 0;
        return minimumPass && maximumPass;
    }

    internal sealed record FeelRangeBounds(object? Minimum, object? Maximum, bool IncludeMinimum, bool IncludeMaximum);
}
