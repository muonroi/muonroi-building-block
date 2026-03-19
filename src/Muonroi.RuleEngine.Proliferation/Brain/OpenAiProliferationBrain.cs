using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Brain implementation using OpenAI-compatible chat completions API.
/// Works with OpenAI, Azure OpenAI, LM Studio, vLLM, or any compatible endpoint.
/// </summary>
public sealed class OpenAiProliferationBrain(
    IHttpClientFactory httpClientFactory,
    ProliferationOptions options,
    IPromptBuilder promptBuilder,
    IMLog<OpenAiProliferationBrain>? logger = null) : IRuleProliferationBrain
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
                AiModelUsed = options.OpenAiModel,
                Scenarios = [],
                GenerationDuration = TimeSpan.Zero
            };
        }

        RuleSetSchema schema = RuleSetSchemaExtractor.Extract(ruleSetJson, context.RuleSetKind);
        string systemPrompt = promptBuilder.BuildSystemPrompt(context.RuleSetKind);
        string userPrompt = promptBuilder.BuildUserPrompt(ruleSetJson, executionResult, factBagSnapshot, budget, context.FocusAreas, schema, context.FlowAnalysis, context.CrossRuleAnalysis);
        Stopwatch sw = Stopwatch.StartNew();

        string? aiResponse = await CallOpenAiAsync(systemPrompt, userPrompt, ct);
        sw.Stop();

        if (aiResponse is null)
        {
            logger?.Error(null, "OpenAI request failed for seed rule {SeedRule}", seedRuleCode);
            return new ProliferationPlan
            {
                SeedRuleCode = seedRuleCode,
                Scope = context.Scope,
                AiModelUsed = options.OpenAiModel,
                Scenarios = [],
                GenerationDuration = sw.Elapsed
            };
        }

        IReadOnlyList<NeuronScenario> scenarios = ScenarioParser.Parse(aiResponse, seedRuleCode, context);

        return new ProliferationPlan
        {
            SeedRuleCode = seedRuleCode,
            Scope = context.Scope,
            AiModelUsed = options.OpenAiModel,
            Scenarios = scenarios,
            GenerationDuration = sw.Elapsed
        };
    }

    private async Task<string?> CallOpenAiAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.AiTimeoutSeconds));

            HttpClient client = httpClientFactory.CreateClient("OpenAiProliferation");
            string endpoint = $"{options.OpenAiEndpoint.TrimEnd('/')}/v1/chat/completions";

            var requestBody = new
            {
                model = options.OpenAiModel,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = options.Temperature,
                max_tokens = options.MaxTokens,
                response_format = new { type = "json_object" }
            };

            using HttpRequestMessage request = new(HttpMethod.Post, endpoint);
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            if (!string.IsNullOrWhiteSpace(options.OpenAiApiKey))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.OpenAiApiKey);
            }

            HttpResponseMessage response = await client.SendAsync(request, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            using JsonDocument doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(timeoutCts.Token),
                cancellationToken: timeoutCts.Token);

            // Extract choices[0].message.content
            if (doc.RootElement.TryGetProperty("choices", out JsonElement choices)
                && choices.GetArrayLength() > 0)
            {
                JsonElement firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out JsonElement message)
                    && message.TryGetProperty("content", out JsonElement content))
                {
                    return content.GetString();
                }
            }

            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger?.Warn("OpenAI request timed out after {Timeout}s", options.AiTimeoutSeconds);
            return null;
        }
        catch (Exception ex)
        {
            logger?.Warn("OpenAI request failed: {Error}", ex.Message);
            return null;
        }
    }
}
