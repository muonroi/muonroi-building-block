using System.Text.Json;
using System.Text.Json.Serialization;

namespace Muonroi.RuleGen.Cli;

internal sealed class CommandContext
{
    public required IReadOnlyDictionary<string, string> RawOptions { get; init; }
    public required RuleGenConfig Config { get; init; }
    public required string WorkingDirectory { get; init; }

    public static CommandContext Create(IReadOnlyDictionary<string, string> raw)
    {
        string workingDir = Directory.GetCurrentDirectory();
        string? configPath = ResolveConfigPath(raw, workingDir);
        RuleGenConfig config = LoadConfig(configPath);

        return new CommandContext
        {
            RawOptions = raw,
            Config = config,
            WorkingDirectory = workingDir
        };
    }

    private static string? ResolveConfigPath(IReadOnlyDictionary<string, string> raw, string workingDir)
    {
        if (raw.TryGetValue("config", out string? explicitPath) && !string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath, workingDir);
        }

        string[] candidates =
        [
            Path.Combine(workingDir, ".rulegenrc.json"),
            Path.Combine(workingDir, ".rulegen.json")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static RuleGenConfig LoadConfig(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new RuleGenConfig();
        }

        string json = File.ReadAllText(path);
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        RuleGenConfig config = JsonSerializer.Deserialize<RuleGenConfig>(json, options) ?? new RuleGenConfig();
        config.ConfigPath = path;
        return config;
    }
}

internal sealed class RuleGenConfig
{
    [JsonIgnore]
    public string? ConfigPath { get; set; }

    public RuleGenExtractConfig Extract { get; set; } = new();
    public RuleGenConventionsConfig Conventions { get; set; } = new();
    public RuleGenValidationConfig Validation { get; set; } = new();
}

internal sealed class RuleGenExtractConfig
{
    public string? Source { get; set; }
    public string? SourceDir { get; set; }
    public string? Project { get; set; }
    public string? OutputDir { get; set; }
    public string? Namespace { get; set; }
    public string? ContextType { get; set; }
    public string? Pattern { get; set; }
    public List<string> ExcludePatterns { get; set; } = [];
    public bool GenerateTests { get; set; }
    public bool Validate { get; set; } = true;
    public bool OrganizeByNamespace { get; set; }
    public bool Parallel { get; set; } = true;
}

internal sealed class RuleGenConventionsConfig
{
    public string? RuleCodePrefix { get; set; }
    public string? DefaultHookPoint { get; set; }
    public bool RequirePartialForMerge { get; set; } = true;
}

internal sealed class RuleGenValidationConfig
{
    public bool DetectCycles { get; set; } = true;
    public bool RequireUniqueCode { get; set; } = true;
    public bool RequireXmlDocs { get; set; }
}
