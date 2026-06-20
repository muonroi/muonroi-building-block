using System.Net.Http;
using System.Text;
using System.Text.Json;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Experience.Abstractions;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Experience.Runtime.Brain;

/// <summary>
/// IExperienceBrain implementation using the Anthropic Claude Messages API.
/// Sends a mistake signal context and parses the response into a NeuronExperience.
/// </summary>
public sealed class ClaudeExperienceBrain(
    IHttpClientFactory httpClientFactory,
    ExperienceBrainOptions options,
    IMLog<ClaudeExperienceBrain>? logger = null) : IExperienceBrain
{
    private const string SystemPrompt =
        "You are an experience extractor. Given a failure trajectory from an AI coding session, " +
        "extract a single structured experience entry. " +
        "Respond ONLY in JSON: {\"trigger\":\"\",\"question\":\"\",\"reasoning\":[],\"solution\":\"\"}";

    private const string AbstractionSystemPrompt =
        "You are a principle extractor. Given several related coding experiences, " +
        "extract ONE general principle that covers all cases. " +
        "Respond ONLY in JSON: {\"trigger\":\"\",\"question\":\"\",\"reasoning\":[],\"solution\":\"\"}";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Extracts NeuronExperience entries from the session log via Claude.</summary>
    public async Task<IEnumerable<NeuronExperience>> ExtractAsync(string sessionLog, CancellationToken ct = default)
    {
        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.AiTimeoutSeconds));

            HttpClient client = httpClientFactory.CreateClient("ClaudeExperienceBrain");

            var requestBody = new
            {
                model = options.ClaudeModel,
                system = SystemPrompt,
                messages = new object[]
                {
                    new { role = "user", content = sessionLog }
                },
                max_tokens = options.MaxTokens,
                temperature = options.Temperature
            };

            using HttpRequestMessage request = new(HttpMethod.Post, $"{options.ClaudeEndpoint.TrimEnd('/')}/v1/messages");
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOpts),
                Encoding.UTF8,
                "application/json");

            if (!string.IsNullOrWhiteSpace(options.ClaudeApiKey))
                request.Headers.Add("x-api-key", options.ClaudeApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            HttpResponseMessage response = await client.SendAsync(request, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            using JsonDocument doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(timeoutCts.Token),
                cancellationToken: timeoutCts.Token);

            // Extract content[0].text
            if (!doc.RootElement.TryGetProperty("content", out JsonElement contentArr)
                || contentArr.GetArrayLength() == 0)
            {
                logger?.Warn("ClaudeExperienceBrain: response has no content array");
                return [];
            }

            JsonElement firstBlock = contentArr[0];
            if (!firstBlock.TryGetProperty("text", out JsonElement textEl))
            {
                logger?.Warn("ClaudeExperienceBrain: content[0] has no text property");
                return [];
            }

            string? text = textEl.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return [];

            return ParseExperience(text, "claude-brain");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger?.Warn("ClaudeExperienceBrain: request timed out after {Timeout}s", options.AiTimeoutSeconds);
            return [];
        }
        catch (Exception ex)
        {
            logger?.Warn("ClaudeExperienceBrain: request failed — {Error}", ex.Message);
            return [];
        }
    }

    /// <summary>Generates a single generalized principle from the pre-formatted abstraction prompt.</summary>
    public async Task<NeuronExperience> AbstractAsync(string abstractionPrompt, CancellationToken ct = default)
    {
        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.AiTimeoutSeconds));

            HttpClient client = httpClientFactory.CreateClient("ClaudeExperienceBrain");

            var requestBody = new
            {
                model = options.ClaudeModel,
                system = AbstractionSystemPrompt,
                messages = new object[]
                {
                    new { role = "user", content = abstractionPrompt }
                },
                max_tokens = options.MaxTokens,
                temperature = options.Temperature
            };

            using HttpRequestMessage request = new(HttpMethod.Post, $"{options.ClaudeEndpoint.TrimEnd('/')}/v1/messages");
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOpts),
                Encoding.UTF8,
                "application/json");

            if (!string.IsNullOrWhiteSpace(options.ClaudeApiKey))
                request.Headers.Add("x-api-key", options.ClaudeApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            HttpResponseMessage response = await client.SendAsync(request, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            using JsonDocument doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(timeoutCts.Token),
                cancellationToken: timeoutCts.Token);

            MGuard.State(
                doc.RootElement.TryGetProperty("content", out JsonElement contentArr) && contentArr.GetArrayLength() > 0,
                "ClaudeExperienceBrain: AbstractAsync returned no content");

            JsonElement firstBlock = contentArr[0];
            MGuard.State(firstBlock.TryGetProperty("text", out JsonElement textEl),
                "ClaudeExperienceBrain: AbstractAsync content[0] has no text");

            string? text = textEl.GetString();
            MGuard.State(!string.IsNullOrWhiteSpace(text),
                "ClaudeExperienceBrain: AbstractAsync returned empty text");

            NeuronExperience? principle = ParseExperience(MGuard.NotNull(text), "claude-brain").FirstOrDefault();
            MGuard.State(principle is not null, "ClaudeExperienceBrain: AbstractAsync could not parse principle JSON");
            return MGuard.NotNull(principle);
        }
        catch (MInternalException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new MInternalException($"ClaudeExperienceBrain: AbstractAsync timed out after {options.AiTimeoutSeconds}s");
        }
        catch (Exception ex)
        {
            throw new MInternalException($"ClaudeExperienceBrain: AbstractAsync failed — {ex.Message}");
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
                logger?.Warn("ClaudeExperienceBrain: parsed JSON missing required fields");
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
                    Trigger = MGuard.NotNull(trigger),
                    Question = MGuard.NotNull(question),
                    Reasoning = reasoning,
                    Solution = MGuard.NotNull(solution),
                    CreatedFrom = createdFrom,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Tier = ExperienceTier.SelfQA,
                    Confidence = 0.5f
                }
            ];
        }
        catch (JsonException ex)
        {
            logger?.Warn("ClaudeExperienceBrain: JSON parse failed — {Error}", ex.Message);
            return [];
        }
    }
}
