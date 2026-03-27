using System.Text;
using System.Text.Json;
using Muonroi.Logging.Abstractions;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Embeds text using Ollama's /api/embed endpoint.
/// Returns empty array on failure (dedup falls back to hash-only).
/// </summary>
public sealed class OllamaEmbedder(
    IHttpClientFactory httpClientFactory,
    ProliferationOptions options,
    IMLog<OllamaEmbedder>? logger = null) : IVectorEmbedder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Embeds text into a vector using Ollama.</summary>
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        try
        {
            HttpClient client = httpClientFactory.CreateClient("OllamaProliferation");
            string endpoint = $"{options.OllamaEndpoint.TrimEnd('/')}/api/embed";

            var requestBody = new
            {
                model = options.EmbeddingModel,
                input = text
            };

            using StringContent content = new(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            HttpResponseMessage response = await client.PostAsync(endpoint, content, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            using JsonDocument doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(timeoutCts.Token),
                cancellationToken: timeoutCts.Token);

            // Ollama returns { "embeddings": [[...]] }
            if (doc.RootElement.TryGetProperty("embeddings", out JsonElement embeddings)
                && embeddings.ValueKind == JsonValueKind.Array
                && embeddings.GetArrayLength() > 0)
            {
                JsonElement firstEmbedding = embeddings[0];
                float[] vector = new float[firstEmbedding.GetArrayLength()];
                int i = 0;
                foreach (JsonElement val in firstEmbedding.EnumerateArray())
                {
                    vector[i++] = val.GetSingle();
                }
                return vector;
            }

            return [];
        }
        catch (Exception ex)
        {
            logger?.Warn("Ollama embedding failed: {Error}", ex.Message);
            return [];
        }
    }
}
