namespace Muonroi.RuleEngine.Proliferation.Tests.Integration;

[Trait("Category", "Integration")]
public class ProliferationIntegrationTests
{
    private const string OllamaEndpoint = "http://127.0.0.1:11434";

    private static bool IsOllamaAvailable()
    {
        try
        {
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(3) };
            HttpResponseMessage response = client.GetAsync($"{OllamaEndpoint}/api/version").Result;
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task OllamaBrain_GeneratesScenariosFromRealModel()
    {
        if (!IsOllamaAvailable())
        {
            // Skip when Ollama is not available
            return;
        }

        ProliferationOptions options = new()
        {
            OllamaEndpoint = OllamaEndpoint,
            PrimaryModel = "qwen2.5-coder:7b-instruct-q5_K_M",
            FallbackModel = "qwen2.5-coder:7b-instruct-q5_K_M",
            AiTimeoutSeconds = 120,
            Temperature = 0.7f,
            MaxTokens = 2000,
            MaxScenariosPerRule = 5
        };

        IHttpClientFactory factory = new SimpleHttpClientFactory();
        DefaultPromptBuilder promptBuilder = new();
        OllamaProliferationBrain brain = new(factory, options, promptBuilder);

        string seedRuleJson = """
            {
                "workflowName": "ORDER_VALIDATE",
                "rules": [
                    {
                        "ruleCode": "VALIDATE_AMOUNT",
                        "condition": "amount > 0 && amount <= 10000",
                        "outputFields": { "isValid": true }
                    },
                    {
                        "ruleCode": "VALIDATE_CURRENCY",
                        "condition": "currency in ['USD', 'EUR', 'VND']",
                        "outputFields": { "currencyValid": true }
                    }
                ]
            }
            """;

        ProliferationPlan plan = await brain.AnalyzeAsync(
            "ORDER_VALIDATE",
            seedRuleJson,
            executionResult: null,
            factBagSnapshot: null,
            new ProliferationContext
            {
                Scope = ProliferationScope.Rule,
                CurrentDepth = 0,
                RemainingBudget = 5
            });

        plan.Should().NotBeNull();
        plan.SeedRuleCode.Should().Be("ORDER_VALIDATE");
        plan.GenerationDuration.Should().BeGreaterThan(TimeSpan.Zero);

        // AI should generate at least 1 scenario
        plan.Scenarios.Should().NotBeEmpty("Ollama should generate test scenarios");

        foreach (NeuronScenario scenario in plan.Scenarios)
        {
            scenario.ScenarioName.Should().NotBeNullOrWhiteSpace();
            scenario.ProliferationReason.Should().NotBeNullOrWhiteSpace();
            scenario.SeedRuleCode.Should().Be("ORDER_VALIDATE");
            scenario.Status.Should().Be(ScenarioStatus.Pending);
        }
    }

    private sealed class SimpleHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
