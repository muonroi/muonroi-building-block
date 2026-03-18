namespace Muonroi.RuleEngine.Proliferation;

public sealed class ProliferationOptions
{
    public const string SectionName = "Proliferation";

    public string OllamaEndpoint { get; set; } = "http://127.0.0.1:11434";
    public string PrimaryModel { get; set; } = "qwen2.5-coder:14b-instruct-q5_K_M";
    public string FallbackModel { get; set; } = "qwen2.5-coder:7b-instruct-q5_K_M";
    public int MaxGenerationDepth { get; set; } = 5;
    public int MaxScenariosPerRule { get; set; } = 20;
    public int MaxTotalScenarios { get; set; } = 500;
    public int WorkerIntervalSeconds { get; set; } = 300;
    public int AiTimeoutSeconds { get; set; } = 120;
    public int ScenarioExecutionTimeoutSeconds { get; set; } = 30;
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 2000;
}
