using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

public sealed class OllamaProliferationBrain(
    IHttpClientFactory httpClientFactory,
    ProliferationOptions options,
    IPromptBuilder promptBuilder,
    IMLog<OllamaProliferationBrain>? logger = null) : IRuleProliferationBrain
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

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

        string systemPrompt = promptBuilder.BuildSystemPrompt(context.RuleSetKind);
        string userPrompt = promptBuilder.BuildUserPrompt(ruleSetJson, executionResult, factBagSnapshot, budget, context.FocusAreas);
        Stopwatch sw = Stopwatch.StartNew();

        // Try primary model, fallback on failure
        string model = options.PrimaryModel;
        string? aiResponse = await CallOllamaAsync(model, systemPrompt, userPrompt, ct);

        if (aiResponse is null && !string.IsNullOrWhiteSpace(options.FallbackModel))
        {
            logger?.Warn("Primary model {Model} failed, falling back to {Fallback}", model, options.FallbackModel);
            model = options.FallbackModel;
            aiResponse = await CallOllamaAsync(model, systemPrompt, userPrompt, ct);
        }

        sw.Stop();

        if (aiResponse is null)
        {
            logger?.Error(null, "Both primary and fallback models failed for seed rule {SeedRule}", seedRuleCode);
            return new ProliferationPlan
            {
                SeedRuleCode = seedRuleCode,
                Scope = context.Scope,
                AiModelUsed = model,
                Scenarios = [],
                GenerationDuration = sw.Elapsed
            };
        }

        IReadOnlyList<NeuronScenario> scenarios = ScenarioParser.Parse(aiResponse, seedRuleCode, context);

        return new ProliferationPlan
        {
            SeedRuleCode = seedRuleCode,
            Scope = context.Scope,
            AiModelUsed = model,
            Scenarios = scenarios,
            GenerationDuration = sw.Elapsed
        };
    }

    private async Task<string?> CallOllamaAsync(string model, string systemPrompt, string userPrompt, CancellationToken ct)
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
                system = systemPrompt,
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
            logger?.Warn("Ollama request timed out after {Timeout}s for model {Model}",
                options.AiTimeoutSeconds, model);
            return null;
        }
        catch (Exception ex)
        {
            logger?.Warn("Ollama request failed for model {Model}: {Error}", model, ex.Message);
            return null;
        }
    }
}
