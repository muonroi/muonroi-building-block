using System.Text.RegularExpressions;
using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Converters;

internal static partial class DecisionTableExpressionEvaluator
{
    [GeneratedRegex(@"^(?<op>>=|<=|!=|=|>|<)\s*(?<value>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex OperatorRegex();

    [GeneratedRegex(@"^(?<open>[\[\(])\s*(?<min>-?\d+(?:\.\d+)?)\s*\.\.\s*(?<max>-?\d+(?:\.\d+)?)\s*(?<close>[\]\)])$",
        RegexOptions.CultureInvariant)]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"^between\s+(?<min>-?\d+(?:\.\d+)?)\s+and\s+(?<max>-?\d+(?:\.\d+)?)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex BetweenRegex();

    public static bool Evaluate(object? actualValue, string expression)
    {
        expression = (expression ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expression) || expression is "-" or "*" or "any")
        {
            return true;
        }

        if (TryEvaluateRange(actualValue, expression, out bool rangeResult))
        {
            return rangeResult;
        }

        if (TryEvaluateBetween(actualValue, expression, out bool betweenResult))
        {
            return betweenResult;
        }

        if (TryEvaluateList(actualValue, expression, out bool listResult))
        {
            return listResult;
        }

        Match op = OperatorRegex().Match(expression);
        if (op.Success)
        {
            return Compare(actualValue, op.Groups["op"].Value, op.Groups["value"].Value);
        }

        return Compare(actualValue, "=", expression);
    }

    public static bool IsExpressionValid(string expression)
    {
        expression = (expression ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expression) || expression is "-" or "*" or "any")
        {
            return true;
        }

        if (RangeRegex().IsMatch(expression) || BetweenRegex().IsMatch(expression) || OperatorRegex().IsMatch(expression))
        {
            return true;
        }

        if (TryEvaluateList("x", expression, out _))
        {
            return true;
        }

        return true;
    }

    public static CellExpression ParseCellExpression(string expression)
    {
        expression = (expression ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expression) || expression is "-" or "*" or "any")
        {
            return new CellExpression
            {
                Raw = expression,
                IsWildcard = true
            };
        }

        Match range = RangeRegex().Match(expression);
        if (range.Success)
        {
            return new CellExpression
            {
                Raw = expression,
                IsRange = true,
                IncludeMin = range.Groups["open"].Value == "[",
                IncludeMax = range.Groups["close"].Value == "]",
                Min = double.Parse(range.Groups["min"].Value, CultureInfo.InvariantCulture),
                Max = double.Parse(range.Groups["max"].Value, CultureInfo.InvariantCulture)
            };
        }

        Match between = BetweenRegex().Match(expression);
        if (between.Success)
        {
            return new CellExpression
            {
                Raw = expression,
                IsRange = true,
                IncludeMin = true,
                IncludeMax = true,
                Min = double.Parse(between.Groups["min"].Value, CultureInfo.InvariantCulture),
                Max = double.Parse(between.Groups["max"].Value, CultureInfo.InvariantCulture)
            };
        }

        Match op = OperatorRegex().Match(expression);
        if (op.Success &&
            double.TryParse(op.Groups["value"].Value.Trim().Trim('\'', '"'), NumberStyles.Float, CultureInfo.InvariantCulture,
                out double operand))
        {
            return op.Groups["op"].Value switch
            {
                ">" => new CellExpression
                {
                    Raw = expression,
                    IsRange = true,
                    Min = operand,
                    Max = null,
                    IncludeMin = false,
                    IncludeMax = true
                },
                ">=" => new CellExpression
                {
                    Raw = expression,
                    IsRange = true,
                    Min = operand,
                    Max = null,
                    IncludeMin = true,
                    IncludeMax = true
                },
                "<" => new CellExpression
                {
                    Raw = expression,
                    IsRange = true,
                    Min = null,
                    Max = operand,
                    IncludeMin = true,
                    IncludeMax = false
                },
                "<=" => new CellExpression
                {
                    Raw = expression,
                    IsRange = true,
                    Min = null,
                    Max = operand,
                    IncludeMin = true,
                    IncludeMax = true
                },
                "=" => new CellExpression
                {
                    Raw = expression,
                    Values = [op.Groups["value"].Value.Trim().Trim('\'', '"')]
                },
                "!=" => new CellExpression
                {
                    Raw = expression,
                    IsWildcard = true
                },
                _ => new CellExpression
                {
                    Raw = expression,
                    Values = [op.Groups["value"].Value.Trim().Trim('\'', '"')]
                }
            };
        }

        if (TryParseListValues(expression, out IReadOnlyList<string>? values))
        {
            return new CellExpression
            {
                Raw = expression,
                Values = values
            };
        }

        return new CellExpression
        {
            Raw = expression,
            Values = [expression]
        };
    }

    private static bool TryEvaluateRange(object? actualValue, string expression, out bool result)
    {
        result = false;
        Match match = RangeRegex().Match(expression);
        if (!match.Success || !TryAsDouble(actualValue, out double actual))
        {
            return false;
        }

        double min = double.Parse(match.Groups["min"].Value, CultureInfo.InvariantCulture);
        double max = double.Parse(match.Groups["max"].Value, CultureInfo.InvariantCulture);
        bool includeMin = match.Groups["open"].Value == "[";
        bool includeMax = match.Groups["close"].Value == "]";

        bool lower = includeMin ? actual >= min : actual > min;
        bool upper = includeMax ? actual <= max : actual < max;
        result = lower && upper;
        return true;
    }

    private static bool TryEvaluateBetween(object? actualValue, string expression, out bool result)
    {
        result = false;
        Match match = BetweenRegex().Match(expression);
        if (!match.Success || !TryAsDouble(actualValue, out double actual))
        {
            return false;
        }

        double min = double.Parse(match.Groups["min"].Value, CultureInfo.InvariantCulture);
        double max = double.Parse(match.Groups["max"].Value, CultureInfo.InvariantCulture);
        result = actual >= min && actual <= max;
        return true;
    }

    private static bool TryEvaluateList(object? actualValue, string expression, out bool result)
    {
        result = false;
        if (!TryParseListValues(expression, out IReadOnlyList<string>? list))
        {
            return false;
        }

        string actual = NormalizeValue(actualValue);
        result = list.Any(x => string.Equals(x, actual, StringComparison.OrdinalIgnoreCase));
        return true;
    }

    private static bool TryParseListValues(string expression, out IReadOnlyList<string> values)
    {
        values = [];
        string normalized = expression.Trim();
        if (normalized.StartsWith("in ", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[3..].Trim();
        }

        if (!normalized.StartsWith("(") || !normalized.EndsWith(")"))
        {
            return false;
        }

        string content = normalized[1..^1];
        string[] parsed = [.. content
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim('\'', '"'))
            .Where(x => !string.IsNullOrWhiteSpace(x))];

        if (parsed.Length == 0)
        {
            return false;
        }

        values = parsed;
        return true;
    }

    private static bool Compare(object? actualValue, string op, string expectedRaw)
    {
        expectedRaw = expectedRaw.Trim().Trim('\'', '"');
        if (TryAsDouble(actualValue, out double actualDouble) &&
            double.TryParse(expectedRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out double expectedDouble))
        {
            return op switch
            {
                ">" => actualDouble > expectedDouble,
                ">=" => actualDouble >= expectedDouble,
                "<" => actualDouble < expectedDouble,
                "<=" => actualDouble <= expectedDouble,
                "!=" => Math.Abs(actualDouble - expectedDouble) > 1e-9,
                _ => Math.Abs(actualDouble - expectedDouble) <= 1e-9
            };
        }

        if (bool.TryParse(expectedRaw, out bool expectedBool) && actualValue is bool actualBool)
        {
            return op == "!=" ? actualBool != expectedBool : actualBool == expectedBool;
        }

        string actualText = NormalizeValue(actualValue);
        return op == "!="
            ? !string.Equals(actualText, expectedRaw, StringComparison.OrdinalIgnoreCase)
            : string.Equals(actualText, expectedRaw, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryAsDouble(object? value, out double result)
    {
        result = default;
        if (value is null)
        {
            return false;
        }

        return double.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static string NormalizeValue(object? value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
