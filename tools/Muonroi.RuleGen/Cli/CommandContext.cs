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

    // ── Auto-register options (run register right after extract) ──

    /// <summary>When true, automatically runs the register step after extraction.</summary>
    public bool AutoRegister { get; set; } = true;

    /// <summary>Registration extension file name.</summary>
    public string RegistrationFileName { get; set; } = "MGeneratedRuleRegistrationExtensions.g.cs";

    /// <summary>Registration extension class name.</summary>
    public string RegistrationClassName { get; set; } = "MGeneratedRuleRegistrationExtensions";

    /// <summary>Namespace for the registration extension. Defaults to the output namespace.</summary>
    public string? RegistrationNamespace { get; set; }

    /// <summary>Generate typed dispatcher interface + implementation per context type.</summary>
    public bool GenerateDispatchers { get; set; } = true;

    /// <summary>Register dispatchers in the DI extension method.</summary>
    public bool RegisterDispatchers { get; set; } = true;

    /// <summary>Include AddRuleEngine&lt;T&gt;() call in registration.</summary>
    public bool IncludeRuleEngine { get; set; } = true;

    /// <summary>Output directory for dispatcher files. Defaults to same as output.</summary>
    public string? DispatcherOutput { get; set; }

    /// <summary>Namespace for dispatcher files. Defaults to registration namespace.</summary>
    public string? DispatcherNamespace { get; set; }

    /// <summary>Overwrite existing dispatcher files.</summary>
    public bool DispatcherOverwrite { get; set; } = true;

    /// <summary>Dispatcher class name suffix.</summary>
    public string DispatcherSuffix { get; set; } = "GeneratedRuleEngineDispatcher";

    /// <summary>Workflow name for version-aware dispatch via RulesEngineService.
    /// When set, the generated dispatcher delegates to RulesEngineService (flow graph + version selection).
    /// When null, the dispatcher delegates directly to RuleOrchestrator (code-first only).</summary>
    public string? WorkflowName { get; set; }
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
