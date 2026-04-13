using System.Text.Json;
using Muonroi.RuleGen.Cli;

namespace Muonroi.RuleGen.Mcp.Infrastructure;

internal static class RuleGenCommandContextFactory
{
    public static CommandContext Create(IReadOnlyDictionary<string, string> rawOptions, string workingDirectory)
    {
        string? configPath = ResolveConfigPath(rawOptions, workingDirectory);
        RuleGenConfig config = LoadConfig(configPath);

        return new CommandContext
        {
            RawOptions = new Dictionary<string, string>(rawOptions, StringComparer.OrdinalIgnoreCase),
            Config = config,
            WorkingDirectory = workingDirectory
        };
    }

    private static string? ResolveConfigPath(IReadOnlyDictionary<string, string> rawOptions, string workingDirectory)
    {
        if (rawOptions.TryGetValue("config", out string? explicitPath) && !string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath, workingDirectory);
        }

        string[] candidates =
        [
            Path.Combine(workingDirectory, ".rulegenrc.json"),
            Path.Combine(workingDirectory, ".rulegen.json")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static RuleGenConfig LoadConfig(string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            return new RuleGenConfig();
        }

        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        string json = File.ReadAllText(configPath);
        RuleGenConfig? config = JsonSerializer.Deserialize<RuleGenConfig>(json, options); // MBB002-exempt: config bootstrap helper
        config ??= new RuleGenConfig();
        config.ConfigPath = configPath;
        return config;
    }
}
