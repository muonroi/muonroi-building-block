namespace Muonroi.Experience.Runtime.Brain;

/// <summary>
/// Configuration options for experience brain implementations (Claude and Ollama).
/// Bind from "ExperienceBrain" configuration section.
/// </summary>
public sealed class ExperienceBrainOptions
{
    /// <summary>Configuration section name for dependency injection binding.</summary>
    public const string SectionName = "ExperienceBrain";

    /// <summary>Anthropic API base endpoint.</summary>
    public string ClaudeEndpoint { get; set; } = "https://api.anthropic.com";

    /// <summary>Anthropic API key (x-api-key header).</summary>
    public string ClaudeApiKey { get; set; } = "";

    /// <summary>Claude model identifier for experience extraction.</summary>
    public string ClaudeModel { get; set; } = "claude-haiku-4-5-20251001";

    /// <summary>Ollama base endpoint.</summary>
    public string OllamaEndpoint { get; set; } = "http://127.0.0.1:11434";

    /// <summary>Primary Ollama model (qwen2.5-coder 14b).</summary>
    public string OllamaPrimaryModel { get; set; } = "qwen2.5-coder:14b-instruct-q5_K_M";

    /// <summary>Fallback Ollama model (qwen2.5-coder 7b).</summary>
    public string OllamaFallbackModel { get; set; } = "qwen2.5-coder:7b-instruct-q5_K_M";

    /// <summary>HTTP request timeout in seconds for AI calls.</summary>
    public int AiTimeoutSeconds { get; set; } = 120;

    /// <summary>Maximum tokens in AI response.</summary>
    public int MaxTokens { get; set; } = 800;

    /// <summary>Sampling temperature (lower = more deterministic).</summary>
    public float Temperature { get; set; } = 0.3f;
}
