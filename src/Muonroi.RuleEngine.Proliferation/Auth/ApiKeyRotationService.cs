using System.Text.Json;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Auth;

/// <summary>
/// Checks API key expiry and rotates the key by calling the rotation URL when within 1 hour of expiry.
/// Gracefully falls back to existing headers if rotation fails.
/// </summary>
/// <remarks>Creates an API key rotation service.</remarks>
public sealed class ApiKeyRotationService(
    IHttpClientFactory httpClientFactory,
    IMLog<ApiKeyRotationService>? logger = null) : IApiKeyRotationService
{
    private static readonly TimeSpan RotationThreshold = TimeSpan.FromHours(1);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IMLog<ApiKeyRotationService>? _logger = logger;

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> CheckAndRotateAsync(
        ExternalProjectConfig config,
        CancellationToken ct)
    {
        IReadOnlyDictionary<string, string> existingHeaders =
            config.ExecutionHeaders ?? new Dictionary<string, string>();

        // No rotation config — return headers as-is
        if (config.ApiKeyRotation == null)
            return existingHeaders;

        // No expiry set — key never expires, no rotation needed
        if (config.ApiKeyRotation.ApiKeyExpiresAt == null)
            return existingHeaders;

        // Check if rotation is needed (within threshold of expiry)
        DateTimeOffset expiresAt = config.ApiKeyRotation.ApiKeyExpiresAt.Value;
        TimeSpan remaining = expiresAt - DateTimeOffset.UtcNow;

        if (remaining > RotationThreshold)
            return existingHeaders;

        // Rotation needed — call rotation URL
        if (string.IsNullOrWhiteSpace(config.ApiKeyRotation.ApiKeyRotationUrl))
        {
            _logger?.Warn("API key rotation needed for project {ProjectId} but no rotation URL configured", config.ProjectId);
            return existingHeaders;
        }

        try
        {
            return await RotateApiKeyAsync(config, existingHeaders, ct);
        }
        catch (Exception ex)
        {
            // Graceful fallback — continue with existing key
            _logger?.Warn("API key rotation failed for project {ProjectId}: {Message}. Using existing key.", config.ProjectId, ex.Message);
            return existingHeaders;
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> RotateApiKeyAsync(
        ExternalProjectConfig config,
        IReadOnlyDictionary<string, string> existingHeaders,
        CancellationToken ct)
    {
        HttpClient client = _httpClientFactory.CreateClient(nameof(ApiKeyRotationService));
        HttpResponseMessage response = await client.GetAsync(MGuard.NotNull(config.ApiKeyRotation).ApiKeyRotationUrl, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger?.Warn("API key rotation endpoint returned {Status} for project {ProjectId}",
                (int)response.StatusCode, config.ProjectId);
            return existingHeaders;
        }

        string json = await response.Content.ReadAsStringAsync(ct);
        using JsonDocument doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("apiKey", out JsonElement apiKeyElem))
        {
            _logger?.Warn("API key rotation response missing 'apiKey' field for project {ProjectId}", config.ProjectId);
            return existingHeaders;
        }

        string newApiKey = apiKeyElem.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newApiKey))
        {
            _logger?.Warn("API key rotation returned empty apiKey for project {ProjectId}", config.ProjectId);
            return existingHeaders;
        }

        // Merge new key into existing headers — replace X-Api-Key or Api-Key if present, otherwise add
        Dictionary<string, string> updatedHeaders = new(existingHeaders, StringComparer.OrdinalIgnoreCase);

        // Find the API key header key (prefer X-Api-Key, fall back to Api-Key, else add X-Api-Key)
        string apiKeyHeader = updatedHeaders.ContainsKey("X-Api-Key") ? "X-Api-Key"
            : updatedHeaders.ContainsKey("Api-Key") ? "Api-Key"
            : "X-Api-Key";

        updatedHeaders[apiKeyHeader] = newApiKey;

        _logger?.Info("API key rotated successfully for project {ProjectId}", config.ProjectId);
        return updatedHeaders;
    }
}
