using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

public sealed class OllamaProliferationBrain(
    IHttpClientFactory httpClientFactory,
    ProliferationOptions options,
    ILogger<OllamaProliferationBrain>? logger = null) : IRuleProliferationBrain
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private const string SystemPrompt = """
        You are a rule proliferation analyzer for a business rule engine.
        Given a business rule definition (flow graph JSON) and optionally its previous execution result,
        generate test scenarios that cover:
        1. Business edge cases: boundary values, null inputs, rare combinations, invalid states
        2. Technical stress cases: concurrent execution, timeout scenarios, error recovery paths

        For each scenario, provide:
        - scenario: descriptive name
        - type: "business" or "technical"
        - reason: why this case matters (1 sentence)
        - inputFacts: JSON object with test input data
        - expectedBehavior: what should happen ("should pass with X", "should fail with Y")

        Respond ONLY with a valid JSON array of scenarios. No markdown, no explanation.
        """;

    public async Task<ProliferationPlan> AnalyzeAsync(
        string seedRuleCode,
        string ruleSetJson,
        JsonElement? executionResult,
        JsonElement? factBagSnapshot,
        ProliferationContext context,
        CancellationToken ct = default)
    {
        int budget = Math.Min(context.RemainingBudget, options.MaxScenariosPerRule);
        if (budget <= 0)
        {
            return new ProliferationPlan
            {
                SeedRuleCode = seedRuleCode,
                Scope = context.Scope,
                AiModelUsed = options.PrimaryModel,
                Scenarios = [],
                GenerationDuration = TimeSpan.Zero
            };
        }

        string userPrompt = BuildUserPrompt(ruleSetJson, executionResult, factBagSnapshot, budget, context.FocusAreas);
        Stopwatch sw = Stopwatch.StartNew();

        // Try primary model, fallback on failure
        string model = options.PrimaryModel;
        string? aiResponse = await CallOllamaAsync(model, userPrompt, ct);

        if (aiResponse is null && !string.IsNullOrWhiteSpace(options.FallbackModel))
        {
            logger?.LogWarning("Primary model {Model} failed, falling back to {Fallback}", model, options.FallbackModel);
            model = options.FallbackModel;
            aiResponse = await CallOllamaAsync(model, userPrompt, ct);
        }

        sw.Stop();

        if (aiResponse is null)
        {
            logger?.LogError("Both primary and fallback models failed for seed rule {SeedRule}", seedRuleCode);
            return new ProliferationPlan
            {
                SeedRuleCode = seedRuleCode,
                Scope = context.Scope,
                AiModelUsed = model,
                Scenarios = [],
                GenerationDuration = sw.Elapsed
            };
        }

        IReadOnlyList<NeuronScenario> scenarios = ParseScenarios(aiResponse, seedRuleCode, context);

        return new ProliferationPlan
        {
            SeedRuleCode = seedRuleCode,
            Scope = context.Scope,
            AiModelUsed = model,
            Scenarios = scenarios,
            GenerationDuration = sw.Elapsed
        };
    }

    private async Task<string?> CallOllamaAsync(string model, string userPrompt, CancellationToken ct)
    {
        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.AiTimeoutSeconds));

            HttpClient client = httpClientFactory.CreateClient("OllamaProliferation");
            string endpoint = $"{options.OllamaEndpoint.TrimEnd('/')}/api/generate";

            var requestBody = new
            {
                model,
                prompt = userPrompt,
                system = SystemPrompt,
                stream = false,
                options = new
                {
                    temperature = options.Temperature,
                    num_predict = options.MaxTokens
                }
            };

            using StringContent content = new(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            HttpResponseMessage response = await client.PostAsync(endpoint, content, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            using JsonDocument doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(timeoutCts.Token),
                cancellationToken: timeoutCts.Token);

            return doc.RootElement.TryGetProperty("response", out JsonElement respEl)
                ? respEl.GetString()
                : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning("Ollama request timed out after {Timeout}s for model {Model}",
                options.AiTimeoutSeconds, model);
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Ollama request failed for model {Model}", model);
            return null;
        }
    }

    private static string BuildUserPrompt(
        string ruleSetJson,
        JsonElement? executionResult,
        JsonElement? factBagSnapshot,
        int budget,
        IReadOnlyList<string>? focusAreas)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Generate {budget} test scenarios.");
        sb.AppendLine();
        sb.AppendLine("Rule definition:");
        sb.AppendLine(ruleSetJson);

        if (executionResult.HasValue)
        {
            sb.AppendLine();
            sb.AppendLine("Previous execution result:");
            sb.AppendLine(executionResult.Value.ToString());
        }

        if (factBagSnapshot.HasValue)
        {
            sb.AppendLine();
            sb.AppendLine("Previous FactBag state:");
            sb.AppendLine(factBagSnapshot.Value.ToString());
        }

        if (focusAreas is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine($"Focus areas: {string.Join(", ", focusAreas)}");
        }

        return sb.ToString();
    }

    internal static IReadOnlyList<NeuronScenario> ParseScenarios(
        string aiResponse,
        string seedRuleCode,
        ProliferationContext context)
    {
        // Strip markdown code fences if AI wraps response
        string json = aiResponse.Trim();
        if (json.StartsWith("```"))
        {
            int firstNewline = json.IndexOf('\n');
            if (firstNewline > 0) json = json[(firstNewline + 1)..];
            if (json.EndsWith("```")) json = json[..^3];
            json = json.Trim();
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

            List<NeuronScenario> scenarios = [];

            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                string? name = item.TryGetProperty("scenario", out JsonElement nameEl) ? nameEl.GetString() : null;
                string? typeStr = item.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() : null;
                string? reason = item.TryGetProperty("reason", out JsonElement reasonEl) ? reasonEl.GetString() : null;
                JsonElement inputFacts = item.TryGetProperty("inputFacts", out JsonElement inputEl)
                    ? inputEl.Clone()
                    : default;
                string? expected = item.TryGetProperty("expectedBehavior", out JsonElement expEl)
                    ? expEl.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(reason)) continue;

                ScenarioType type = typeStr?.Equals("technical", StringComparison.OrdinalIgnoreCase) == true
                    ? ScenarioType.Technical
                    : ScenarioType.Business;

                scenarios.Add(new NeuronScenario
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SeedRuleCode = seedRuleCode,
                    ScenarioName = name,
                    Type = type,
                    Scope = context.Scope,
                    ParentScenarioId = null,
                    GenerationDepth = context.CurrentDepth,
                    ProliferationReason = reason,
                    InputFacts = inputFacts,
                    ExpectedBehavior = expected,
                    Status = ScenarioStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow,
                    TenantId = context.TenantId
                });
            }

            return scenarios;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
