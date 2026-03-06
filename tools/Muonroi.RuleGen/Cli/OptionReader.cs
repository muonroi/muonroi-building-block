namespace Muonroi.RuleGen.Cli;

internal static class OptionReader
{
    public static string? GetString(CommandContext context, string key, string? fallback = null)
    {
        if (context.RawOptions.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return fallback;
    }

    public static bool GetBool(CommandContext context, string key, bool fallback)
    {
        if (context.RawOptions.TryGetValue(key, out string? value))
        {
            if (bool.TryParse(value, out bool parsed))
            {
                return parsed;
            }

            if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "y", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "n", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return fallback;
    }

    public static IReadOnlyList<string> GetCsvList(CommandContext context, string key, IReadOnlyList<string>? fallback = null)
    {
        if (context.RawOptions.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
        {
            return value
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
        }

        return fallback ?? [];
    }
}
