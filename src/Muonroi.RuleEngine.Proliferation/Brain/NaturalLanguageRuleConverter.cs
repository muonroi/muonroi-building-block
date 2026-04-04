using System.Text;
using System.Text.Json;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Result of a natural language to ruleset conversion.
/// </summary>
public sealed record NlConversionResult
{
    /// <summary>Indicates whether the conversion succeeded.</summary>
    public bool Success { get; init; }
    /// <summary>Generated ruleset JSON, when conversion succeeds.</summary>
    public string? RuleSetJson { get; init; }
    /// <summary>Detected ruleset kind.</summary>
    public RuleSetKind DetectedKind { get; init; }
    /// <summary>Error message when conversion fails.</summary>
    public string? ErrorMessage { get; init; }
    /// <summary>Schema extracted from the generated ruleset.</summary>
    public RuleSetSchema? ExtractedSchema { get; init; }
}

/// <summary>
/// Request to convert a natural language description into a structured ruleset.
/// </summary>
public sealed record NlConversionRequest
{
    /// <summary>Natural language description of the desired business rule.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Desired output format: "flowgraph" (default) or "decisiontable".
    /// </summary>
    public string OutputFormat { get; init; } = "flowgraph";

    /// <summary>
    /// Optional workflow name to associate the converted ruleset with.
    /// </summary>
    public string? WorkflowName { get; init; }
}

/// <summary>
/// Converts natural language business rule descriptions into structured ruleset JSON
/// (RuleFlowGraph or DecisionTable) via the configured AI backend.
/// </summary>
public interface INaturalLanguageRuleConverter
{
    /// <summary>Converts a natural language request into structured ruleset JSON.</summary>
    Task<NlConversionResult> ConvertAsync(NlConversionRequest request, CancellationToken ct = default);
}

/// <summary>
/// Implementation of <see cref="INaturalLanguageRuleConverter"/> that uses
/// the same Ollama streaming endpoint as <see cref="OllamaProliferationBrain"/>.
/// Retries up to 2 times if the AI response is not valid JSON.
/// </summary>
public sealed class NaturalLanguageRuleConverter(
    IHttpClientFactory httpClientFactory,
    ProliferationOptions options) : INaturalLanguageRuleConverter
{
    private const int MaxRetries = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Converts a natural language request into structured ruleset JSON.</summary>
    public async Task<NlConversionResult> ConvertAsync(NlConversionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return new NlConversionResult
            {
                Success = false,
                ErrorMessage = "Description must not be empty."
            };
        }

        RuleSetKind kind = string.Equals(request.OutputFormat, "decisiontable", StringComparison.OrdinalIgnoreCase)
            ? RuleSetKind.DecisionTable
            : RuleSetKind.FlowGraph;

        string systemPrompt = BuildSystemPrompt();
        string userPrompt = BuildUserPrompt(request.Description, request.OutputFormat);

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            string? rawResponse = await CallOllamaAsync(options.PrimaryModel, systemPrompt, userPrompt, ct);

            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                // Try fallback model if available
                if (attempt == 0 && !string.IsNullOrWhiteSpace(options.FallbackModel))
                {
                    rawResponse = await CallOllamaAsync(options.FallbackModel, systemPrompt, userPrompt, ct);
                }

                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    continue;
                }
            }

            // Extract JSON from response (AI may wrap it in markdown code blocks)
            string? extractedJson = ExtractJson(rawResponse);
            if (extractedJson is null) continue;

            // Validate JSON
            if (!TryParseJson(extractedJson, out _)) continue;

            // Extract schema to validate and enrich the result
            RuleSetSchema schema = RuleSetSchemaExtractor.Extract(extractedJson, kind);

            return new NlConversionResult
            {
                Success = true,
                RuleSetJson = extractedJson,
                DetectedKind = kind,
                ErrorMessage = null,
                ExtractedSchema = schema
            };
        }

        return new NlConversionResult
        {
            Success = false,
            ErrorMessage = $"Failed to generate valid {request.OutputFormat} JSON after {MaxRetries + 1} attempts. " +
                           "The AI model returned invalid or non-JSON output.",
            DetectedKind = kind
        };
    }

    // ------------------------------------------------------------------
    // Prompt construction
    // ------------------------------------------------------------------

    private static string BuildSystemPrompt() =>
        "You are a business rule converter. " +
        "Convert natural language descriptions into structured rule definitions. " +
        "Output ONLY valid JSON - no markdown, no explanation, no code fences.";

    private static string BuildUserPrompt(string description, string outputFormat)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Convert the following business rule description to a {outputFormat} JSON definition:");
        sb.AppendLine();
        sb.AppendLine($"Description: {description}");
        sb.AppendLine();

        if (string.Equals(outputFormat, "decisiontable", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("Output a JSON object with this structure:");
            sb.AppendLine(@"{
  ""inputColumns"": [{ ""name"": ""fieldName"", ""dataType"": ""number|string|boolean"", ""isRequired"": true }],
  ""outputColumns"": [{ ""name"": ""resultField"", ""dataType"": ""number|string|boolean"" }],
  ""rules"": [{ ""conditions"": [""condition expression""], ""actions"": [""output value""] }]
}");
        }
        else
        {
            sb.AppendLine("Output a JSON object with this structure:");
            sb.AppendLine(@"{
  ""nodes"": [
    { ""id"": ""start"", ""type"": ""Start"", ""data"": { ""triggerData"": { ""inputSchema"": { ""fieldName"": ""dataType"" } } } },
    { ""id"": ""rule1"", ""type"": ""RuleTask"", ""data"": { ""condition"": ""input.fieldName > value"", ""outputFields"": [{ ""path"": ""outputField"", ""dataType"": ""number"" }] } },
    { ""id"": ""end"", ""type"": ""End"", ""data"": {} }
  ],
  ""edges"": [
    { ""source"": ""start"", ""target"": ""rule1"", ""type"": ""always"" },
    { ""source"": ""rule1"", ""target"": ""end"", ""type"": ""always"" }
  ]
}");
        }

        sb.AppendLine();
        sb.AppendLine("Use field names derived from the description (camelCase, no spaces). Output ONLY the JSON, nothing else.");

        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Ollama HTTP call (streaming, same pattern as OllamaProliferationBrain)
    // ------------------------------------------------------------------

    private async Task<string?> CallOllamaAsync(string model, string systemPrompt, string userPrompt, CancellationToken ct)
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

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, endpoint) { Content = content };
            HttpResponseMessage response = await client.SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

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
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------
    // JSON extraction helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Extracts the first JSON object from the response string.
    /// Handles responses wrapped in markdown code fences (```json ... ```).
    /// </summary>
    private static string? ExtractJson(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;

        // Strip markdown code fences
        string cleaned = response.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = cleaned.IndexOf('\n');
            if (firstNewline >= 0)
                cleaned = cleaned[(firstNewline + 1)..];

            int lastFence = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0)
                cleaned = cleaned[..lastFence];

            cleaned = cleaned.Trim();
        }

        // Find the first { and matching }
        int start = cleaned.IndexOf('{');
        if (start < 0) return null;

        int depth = 0;
        for (int i = start; i < cleaned.Length; i++)
        {
            if (cleaned[i] == '{') depth++;
            else if (cleaned[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return cleaned[start..(i + 1)];
            }
        }

        return null;
    }

    private static bool TryParseJson(string json, out JsonDocument? doc)
    {
        try
        {
            doc = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            doc = null;
            return false;
        }
    }
}
