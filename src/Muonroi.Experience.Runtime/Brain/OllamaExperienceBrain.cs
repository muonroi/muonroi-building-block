using System.Net.Http;
using System.Text;
using System.Text.Json;
using Muonroi.Experience.Abstractions;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Experience.Runtime.Brain;

/// <summary>
/// IExperienceBrain implementation using the local Ollama /api/generate endpoint.
/// Reads streaming NDJSON and accumulates the response field into NeuronExperience.
/// </summary>
public sealed class OllamaExperienceBrain(
    IHttpClientFactory httpClientFactory,
    ExperienceBrainOptions options,
    IMLog<OllamaExperienceBrain>? logger = null) : IExperienceBrain
{
    private const string ExtractionPromptPrefix =
        "You are an experience extractor. Given a failure trajectory from an AI coding session, " +
        "extract a single structured experience entry. " +
        "Respond ONLY in JSON: {\"trigger\":\"\",\"question\":\"\",\"reasoning\":[],\"solution\":\"\"}. " +
        "Session log:\n";

    private const string AbstractionPromptPrefix =
        "You are a principle extractor. Given several related coding experiences, " +
        "extract ONE general principle that covers all cases. " +
        "Respond ONLY in JSON: {\"trigger\":\"\",\"question\":\"\",\"reasoning\":[],\"solution\":\"\"}. " +
        "Experiences:\n";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Extracts NeuronExperience entries from the session log via Ollama.</summary>
    public async Task<IEnumerable<NeuronExperience>> ExtractAsync(string sessionLog, CancellationToken ct = default)
    {
        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.AiTimeoutSeconds));

            HttpClient client = httpClientFactory.CreateClient("OllamaExperienceBrain");

            var requestBody = new
            {
                model = options.OllamaPrimaryModel,
                prompt = ExtractionPromptPrefix + sessionLog,
                stream = true,
                options = new
                {
                    temperature = options.Temperature,
                    num_predict = options.MaxTokens
                }
            };

            using StringContent content = new(
                JsonSerializer.Serialize(requestBody, JsonOpts),
                Encoding.UTF8,
                "application/json");

            using HttpRequestMessage request = new(HttpMethod.Post, $"{options.OllamaEndpoint.TrimEnd('/')}/api/generate")
            {
                Content = content
            };

            HttpResponseMessage response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            // Accumulate streaming NDJSON response fields
            var accumulated = new StringBuilder();
            using Stream stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            using var reader = new StreamReader(stream);

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
                    // Skip malformed chunk lines
                }
            }

            string accumulated_str = accumulated.ToString();
            if (string.IsNullOrWhiteSpace(accumulated_str))
                return [];

            return ParseExperience(accumulated_str, "ollama-brain");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger?.Warn("OllamaExperienceBrain: request timed out after {Timeout}s", options.AiTimeoutSeconds);
            return [];
        }
        catch (Exception ex)
        {
            logger?.Warn("OllamaExperienceBrain: request failed — {Error}", ex.Message);
            return [];
        }
    }

    /// <summary>Generates a single generalized principle via Ollama using a dedicated abstraction system prompt.</summary>
    public async Task<NeuronExperience> AbstractAsync(string abstractionPrompt, CancellationToken ct = default)
    {
        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.AiTimeoutSeconds));

            HttpClient client = httpClientFactory.CreateClient("OllamaExperienceBrain");

            var requestBody = new
            {
                model = options.OllamaPrimaryModel,
                prompt = AbstractionPromptPrefix + abstractionPrompt,
                stream = true,
                options = new
                {
                    temperature = options.Temperature,
                    num_predict = options.MaxTokens
                }
            };

            using StringContent content = new(
                JsonSerializer.Serialize(requestBody, JsonOpts),
                Encoding.UTF8,
                "application/json");

            using HttpRequestMessage request = new(HttpMethod.Post, $"{options.OllamaEndpoint.TrimEnd('/')}/api/generate")
            {
                Content = content
            };

            HttpResponseMessage response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            var accumulated = new StringBuilder();
            using Stream stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            using var reader = new StreamReader(stream);

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
                    // Skip malformed chunk lines
                }
            }

            string result = accumulated.ToString();
            if (string.IsNullOrWhiteSpace(result))
                throw new InvalidOperationException("OllamaExperienceBrain: AbstractAsync returned empty response");

            NeuronExperience? principle = ParseExperience(result, "ollama-brain").FirstOrDefault();
            return principle ?? throw new InvalidOperationException("OllamaExperienceBrain: AbstractAsync could not parse principle JSON");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException($"OllamaExperienceBrain: AbstractAsync timed out after {options.AiTimeoutSeconds}s");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"OllamaExperienceBrain: AbstractAsync failed — {ex.Message}", ex);
        }
    }

    private IEnumerable<NeuronExperience> ParseExperience(string json, string createdFrom)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            string? trigger = root.TryGetProperty("trigger", out JsonElement t) ? t.GetString() : null;
            string? question = root.TryGetProperty("question", out JsonElement q) ? q.GetString() : null;
            string? solution = root.TryGetProperty("solution", out JsonElement s) ? s.GetString() : null;

            if (string.IsNullOrWhiteSpace(trigger) || string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(solution))
            {
                logger?.Warn("OllamaExperienceBrain: parsed JSON missing required fields");
                return [];
            }

            string[] reasoning = [];
            if (root.TryGetProperty("reasoning", out JsonElement rEl) && rEl.ValueKind == JsonValueKind.Array)
            {
                reasoning = rEl.EnumerateArray()
                    .Select(e => e.GetString() ?? "")
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .ToArray();
            }

            return
            [
                new NeuronExperience
                {
                    Id = Guid.NewGuid().ToString(),
                    Trigger = trigger!,
                    Question = question!,
                    Reasoning = reasoning,
                    Solution = solution!,
                    CreatedFrom = createdFrom,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Tier = ExperienceTier.SelfQA,
                    Confidence = 0.5f
                }
            ];
        }
        catch (JsonException ex)
        {
            logger?.Warn("OllamaExperienceBrain: JSON parse failed — {Error}", ex.Message);
            return [];
        }
    }
}
