using System.Text.RegularExpressions;

namespace Muonroi.RuleEngine.DecisionTable.Feel;

internal sealed partial class FullFeelCellEvaluator(SimplifiedFeelCellEvaluator? fallback = null) : IFeelCellEvaluator
{
    private readonly IFeelCellEvaluator _fallback = fallback ?? new SimplifiedFeelCellEvaluator();

    [GeneratedRegex(@"^(>=|<=|!=|=|>|<)", RegexOptions.CultureInvariant)]
    private static partial Regex OperatorPrefixRegex();

    [GeneratedRegex(@"^\s*[\[\(].*\.\..*[\]\)]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex RangeUnaryRegex();

    public bool Evaluate(string expression, object? inputValue, string? columnDataType = null)
    {
        string normalized = (expression ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized is "-" or "*" or "any")
        {
            return true;
        }

        object? coerced = CoerceValue(inputValue, columnDataType);
        string feelExpression = NormalizeUnaryExpression(normalized);
        Dictionary<string, object> variables = BuildVariables(coerced);

        object? result = FeelEvaluator.EvaluateValue(feelExpression, variables);
        if (result is bool boolResult)
        {
            return boolResult;
        }

        if (result is null)
        {
            return _fallback.Evaluate(normalized, inputValue, columnDataType);
        }

        return EqualsByValue(coerced, result);
    }

    public string? Validate(string expression, string? columnDataType = null)
    {
        string normalized = (expression ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized is "-" or "*" or "any")
        {
            return null;
        }

        string feelExpression = NormalizeUnaryExpression(normalized);
        object? sample = CoerceValue(GetSampleValue(columnDataType), columnDataType);
        Dictionary<string, object> variables = BuildVariables(sample);
        object? result = FeelEvaluator.EvaluateValue(feelExpression, variables);
        if (result is not null)
        {
            return null;
        }

        return _fallback.Validate(normalized, columnDataType) ?? "Invalid FEEL expression.";
    }

    private static string NormalizeUnaryExpression(string expression)
    {
        if (expression.StartsWith("in ", StringComparison.OrdinalIgnoreCase))
        {
            return $"input {expression}";
        }

        if (expression.StartsWith("between ", StringComparison.OrdinalIgnoreCase))
        {
            return $"input {expression}";
        }

        if (OperatorPrefixRegex().IsMatch(expression))
        {
            return $"input {expression}";
        }

        if (RangeUnaryRegex().IsMatch(expression))
        {
            return $"input in {expression}";
        }

        return expression;
    }

    private static Dictionary<string, object> BuildVariables(object? inputValue)
    {
        Dictionary<string, object> variables = new(StringComparer.OrdinalIgnoreCase);
        if (inputValue is not null)
        {
            variables["input"] = inputValue;
            variables["value"] = inputValue;
        }

        return variables;
    }

    private static object? CoerceValue(object? value, string? columnDataType)
    {
        if (value is null || string.IsNullOrWhiteSpace(columnDataType))
        {
            return value;
        }

        string type = columnDataType.Trim().ToLowerInvariant();
        if (value is string raw)
        {
            if (type is "number" or "numeric" && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double n))
            {
                return n;
            }

            if (type is "boolean" or "bool" && bool.TryParse(raw, out bool b))
            {
                return b;
            }

            if (type == "date" && DateOnly.TryParse(raw, CultureInfo.InvariantCulture, out DateOnly dateOnly))
            {
                return dateOnly;
            }

            if ((type is "datetime" or "date-time") &&
                DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime dt))
            {
                return dt;
            }
        }

        return value;
    }

    private static bool EqualsByValue(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (double.TryParse(Convert.ToString(left, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out double leftNum) &&
            double.TryParse(Convert.ToString(right, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out double rightNum))
        {
            return Math.Abs(leftNum - rightNum) <= 1e-9;
        }

        return string.Equals(
            Convert.ToString(left, CultureInfo.InvariantCulture),
            Convert.ToString(right, CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
    }

    private static object? GetSampleValue(string? columnDataType)
    {
        return columnDataType?.Trim().ToLowerInvariant() switch
        {
            "number" or "numeric" => 1d,
            "boolean" or "bool" => true,
            "date" => new DateOnly(2026, 1, 1),
            "datetime" or "date-time" => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => "sample"
        };
    }
}
