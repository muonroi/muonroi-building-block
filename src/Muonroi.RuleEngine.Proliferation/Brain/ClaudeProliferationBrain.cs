using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Brain implementation using Anthropic Messages API.
/// </summary>
public sealed class ClaudeProliferationBrain(
    IHttpClientFactory httpClientFactory,
    ProliferationOptions options,
    IPromptBuilder promptBuilder,
    IMLog<ClaudeProliferationBrain>? logger = null,
    ISyntheticScenarioGenerator? syntheticGen = null) : IRuleProliferationBrain
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
                AiModelUsed = options.ClaudeModel,
                Scenarios = [],
                GenerationDuration = TimeSpan.Zero
            };
        }

        RuleSetSchema schema = RuleSetSchemaExtractor.Extract(ruleSetJson, context.RuleSetKind);
        string systemPrompt = promptBuilder.BuildSystemPrompt(context.RuleSetKind);
        string userPrompt = promptBuilder.BuildUserPrompt(ruleSetJson, executionResult, factBagSnapshot, budget, context.FocusAreas, schema, context.FlowAnalysis, context.CrossRuleAnalysis);
        Stopwatch sw = Stopwatch.StartNew();

        string? aiResponse = await CallClaudeAsync(systemPrompt, userPrompt, ct);
        sw.Stop();

        ISyntheticScenarioGenerator effectiveSyntheticGen = syntheticGen ?? new SyntheticScenarioGenerator();

        IReadOnlyList<NeuronScenario> scenarios;
        if (aiResponse is null)
        {
            logger?.Error(null, "Claude request failed for seed rule {SeedRule}", seedRuleCode);
            scenarios = effectiveSyntheticGen.Generate(seedRuleCode, schema, context);
        }
        else
        {
            IReadOnlyList<NeuronScenario> initial = ScenarioParser.Parse(aiResponse, seedRuleCode, context);
            if (initial.Count > 0)
            {
                scenarios = initial;
            }
            else
            {
                scenarios = await ScenarioParser.ParseWithRetry(
                    async (suffix) => await CallClaudeAsync(systemPrompt, userPrompt + suffix, ct),
                    seedRuleCode, context, effectiveSyntheticGen, schema);
            }
        }

        return new ProliferationPlan
        {
            SeedRuleCode = seedRuleCode,
            Scope = context.Scope,
            AiModelUsed = options.ClaudeModel,
            Scenarios = scenarios,
            GenerationDuration = sw.Elapsed
        };
    }

    private async Task<string?> CallClaudeAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.AiTimeoutSeconds));

            HttpClient client = httpClientFactory.CreateClient("ClaudeProliferation");
            string endpoint = $"{options.ClaudeEndpoint.TrimEnd('/')}/v1/messages";

            var requestBody = new
            {
                model = options.ClaudeModel,
                system = systemPrompt,
                messages = new object[]
                {
                    new { role = "user", content = userPrompt }
                },
                max_tokens = options.MaxTokens,
                temperature = options.Temperature
            };

            using HttpRequestMessage request = new(HttpMethod.Post, endpoint);
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            if (!string.IsNullOrWhiteSpace(options.ClaudeApiKey))
            {
                request.Headers.Add("x-api-key", options.ClaudeApiKey);
            }
            request.Headers.Add("anthropic-version", "2023-06-01");

            HttpResponseMessage response = await client.SendAsync(request, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            using JsonDocument doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(timeoutCts.Token),
                cancellationToken: timeoutCts.Token);

            // Extract content[0].text
            if (doc.RootElement.TryGetProperty("content", out JsonElement contentArr)
                && contentArr.GetArrayLength() > 0)
            {
                JsonElement firstBlock = contentArr[0];
                if (firstBlock.TryGetProperty("text", out JsonElement text))
                {
                    return text.GetString();
                }
            }

            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger?.Warn("Claude request timed out after {Timeout}s", options.AiTimeoutSeconds);
            return null;
        }
        catch (Exception ex)
        {
            logger?.Warn("Claude request failed: {Error}", ex.Message);
            return null;
        }
    }
}
