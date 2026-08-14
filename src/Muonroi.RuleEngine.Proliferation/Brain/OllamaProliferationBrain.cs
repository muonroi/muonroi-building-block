namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Brain implementation using the Ollama API.
/// </summary>
public sealed class OllamaProliferationBrain(
    IHttpClientFactory httpClientFactory,
    ProliferationOptions options,
    IPromptBuilder promptBuilder,
    IMLog<OllamaProliferationBrain>? logger = null,
    ISyntheticScenarioGenerator? syntheticGen = null,
    IInfraHealthMonitor? infraMonitor = null) : IRuleProliferationBrain
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Analyzes a rule set and produces proliferation scenarios.</summary>
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

        RuleSetSchema schema = RuleSetSchemaExtractor.Extract(ruleSetJson, context.RuleSetKind);

        // Infra-aware: adjust budget and select model based on Ollama health
        string model = options.PrimaryModel;
        if (infraMonitor is not null)
        {
            InfraHealthStatus infraStatus = await infraMonitor.CheckHealthAsync(ct);
            budget = (int)Math.Max(1, budget * infraStatus.BudgetMultiplier);
            model = infraStatus.RecommendedModel;
            logger?.Debug("InfraHealth: {Level} | BudgetMultiplier: {Multiplier} → budget={Budget} | Model: {Model}",
                infraStatus.Level, infraStatus.BudgetMultiplier, budget, model);
        }

        string systemPrompt = promptBuilder.BuildSystemPrompt(context.RuleSetKind);
        string userPrompt = promptBuilder.BuildUserPrompt(ruleSetJson, executionResult, factBagSnapshot, budget, context.FocusAreas, schema, context.FlowAnalysis, context.CrossRuleAnalysis);
        Stopwatch sw = Stopwatch.StartNew();
        string? firstResponse = await CallOllamaAsync(model, systemPrompt, userPrompt, ct);

        if (firstResponse is null && !string.IsNullOrWhiteSpace(options.FallbackModel))
        {
            logger?.Warn("Primary model {Model} failed, falling back to {Fallback}", model, options.FallbackModel);
            model = options.FallbackModel;
            firstResponse = await CallOllamaAsync(model, systemPrompt, userPrompt, ct);
        }

        sw.Stop();

        // Use ParseWithRetry: if initial response parses to 0 scenarios, retry with varied prompts,
        // then fall back to synthetic generator if all retries fail.
        ISyntheticScenarioGenerator effectiveSyntheticGen = syntheticGen ?? new SyntheticScenarioGenerator();

        IReadOnlyList<NeuronScenario> scenarios;
        if (firstResponse is null)
        {
            logger?.Error(null, "Both primary and fallback models failed for seed rule {SeedRule}", seedRuleCode);
            // Try synthetic fallback directly
            scenarios = effectiveSyntheticGen.Generate(seedRuleCode, schema, context);
        }
        else
        {
            // Parse initial response; if empty, retry via ParseWithRetry
            IReadOnlyList<NeuronScenario> initial = ScenarioParser.Parse(firstResponse, seedRuleCode, context);
            if (initial.Count > 0)
            {
                scenarios = initial;
            }
            else
            {
                // Retry with escalating prompts
                scenarios = await ScenarioParser.ParseWithRetry(
                    async (suffix) => await CallOllamaAsync(model, systemPrompt, userPrompt + suffix, ct),
                    seedRuleCode, context, effectiveSyntheticGen, schema);
            }
        }

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
        // Try streaming first (better for slow CPU inference), fall back to non-streaming
        string? result = await CallOllamaStreamingAsync(model, systemPrompt, userPrompt, ct);
        return result;
    }

    private async Task<string?> CallOllamaStreamingAsync(string model, string systemPrompt, string userPrompt, CancellationToken ct)
    {
        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.AiTimeoutSeconds));

            HttpClient client = httpClientFactory.CreateClient("OllamaProliferation");
            string endpoint = EndpointValidator.ValidateLocal(options.OllamaEndpoint, "/api/generate");

            var requestBody = new
            {
                model,
                prompt = userPrompt,
                system = systemPrompt,
                stream = true,
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

            using HttpRequestMessage request = new(HttpMethod.Post, endpoint) { Content = content };
            HttpResponseMessage response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            // Read NDJSON stream line-by-line, accumulate response field
            StringBuilder accumulated = new();
            using Stream stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            using StreamReader reader = new(stream);

            while (!reader.EndOfStream && !timeoutCts.Token.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(timeoutCts.Token);
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using JsonDocument chunk = JsonDocument.Parse(line);
                    if (chunk.RootElement.TryGetProperty("response", out JsonElement respEl))
                    {
                        string? token = respEl.GetString();
                        if (token is not null)
                            accumulated.Append(token);
                    }

                    // Check if this is the final chunk
                    if (chunk.RootElement.TryGetProperty("done", out JsonElement doneEl)
                        && doneEl.ValueKind == JsonValueKind.True)
                    {
                        break;
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed chunks
                }
            }

            string result = accumulated.ToString();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger?.Warn("Ollama streaming request timed out after {Timeout}s for model {Model}",
                options.AiTimeoutSeconds, model);
            return null;
        }
        catch (Exception ex)
        {
            logger?.Warn("Ollama streaming request failed for model {Model}: {Error}", model, ex.Message);
            return null;
        }
    }
}
