namespace Muonroi.RuleGen.Services;

internal static partial class FeelCSharpTranslator
{
    [GeneratedRegex("(?<![<>!=])=(?!=)", RegexOptions.CultureInvariant)]
    private static partial Regex SingleEqualsRegex();

    public static string FeelToCSharpCondition(string? feel)
    {
        if (string.IsNullOrWhiteSpace(feel))
        {
            return "true";
        }

        string expr = feel.Trim();
        expr = Regex.Replace(expr, "\\band\\b", "&&", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        expr = Regex.Replace(expr, "\\bor\\b", "||", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        expr = Regex.Replace(expr, "\\bnot\\b", "!", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        expr = SingleEqualsRegex().Replace(expr, "==");
        expr = Regex.Replace(expr, "'(?<text>[^']*)'", m => $"\"{m.Groups["text"].Value}\"", RegexOptions.CultureInvariant);

        expr = Regex.Replace(expr, @"([A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)+)", m => ToCSharpPath(m.Value), RegexOptions.CultureInvariant);
        return expr;
    }

    public static string ActionToCSharp(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return string.Empty;
        }

        string text = action.Trim().TrimEnd(';');
        Match factMatch = Regex.Match(
            text,
            "facts\\s*\\[\\s*['\\\"](?<key>[^'\\\"]+)['\\\"]\\s*\\]\\s*=\\s*(?<value>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (factMatch.Success)
        {
            string key = factMatch.Groups["key"].Value;
            string value = FeelToCSharpCondition(factMatch.Groups["value"].Value);
            return $"facts[\"{key}\"] = {value};";
        }

        return $"// Unsupported action expression: {text}";
    }

    public static string CSharpToFeel(string? csharp)
    {
        if (string.IsNullOrWhiteSpace(csharp))
        {
            return "true";
        }

        string expr = csharp.Trim();
        expr = expr.Replace("&&", " and ", StringComparison.Ordinal);
        expr = expr.Replace("||", " or ", StringComparison.Ordinal);
        expr = expr.Replace("==", " = ", StringComparison.Ordinal);
        expr = Regex.Replace(expr, @"\bctx\.([A-Za-z_][\w\.]*)", m => ToFeelPath(m.Groups[1].Value), RegexOptions.CultureInvariant);
        return Regex.Replace(expr, "\\s+", " ", RegexOptions.CultureInvariant).Trim();
    }

    private static string ToCSharpPath(string value)
    {
        if (value.StartsWith("ctx.", StringComparison.Ordinal))
        {
            return value;
        }

        string[] parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return value;
        }

        string converted = string.Join('.', parts.Select(ToPascalCase));
        return $"ctx.{converted}";
    }

    private static string ToFeelPath(string value)
    {
        string[] parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join('.', parts.Select(ToCamelCase));
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.Length == 1)
        {
            return value.ToUpperInvariant();
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.Length == 1)
        {
            return value.ToLowerInvariant();
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
