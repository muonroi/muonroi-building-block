namespace Muonroi.RuleGen.Cli;

internal static class CommandLineParser
{
    public static IReadOnlyDictionary<string, string> Parse(string[] args)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            string key = token[2..];
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            string value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";
            result[key] = value;
        }

        return result;
    }
}
