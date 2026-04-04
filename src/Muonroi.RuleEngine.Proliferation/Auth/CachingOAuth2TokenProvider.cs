using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Auth;

/// <summary>
/// OAuth2 token provider with in-process caching.
/// Cache key: "{clientId}:{tokenEndpoint}" — allows multiple tenants/configs to coexist.
/// Early refresh: tokens are considered expired (expires_in - 60s buffer) to avoid serving expired tokens.
/// </summary>
/// <remarks>Creates a caching OAuth2 token provider.</remarks>
public sealed class CachingOAuth2TokenProvider(
    IHttpClientFactory httpClientFactory,
    IMLog<CachingOAuth2TokenProvider>? logger = null) : IOAuth2TokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IMLog<CachingOAuth2TokenProvider>? _logger = logger;

    // Key: "{clientId}:{tokenEndpoint}", Value: (access_token, expiresAt with 60s buffer)
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAt)> _cache = new();

    /// <inheritdoc/>
    public async Task<string> GetAccessTokenAsync(OAuth2Config config, CancellationToken ct)
    {
        string cacheKey = BuildCacheKey(config);

        // Return cached token if still valid
        if (_cache.TryGetValue(cacheKey, out var cached) && DateTimeOffset.UtcNow < cached.ExpiresAt)
        {
            return cached.Token;
        }

        // Fetch new token
        string token = await FetchTokenAsync(config, ct);
        return token;
    }

    /// <inheritdoc/>
    public void InvalidateToken(OAuth2Config config)
    {
        string cacheKey = BuildCacheKey(config);
        _cache.TryRemove(cacheKey, out _);
        _logger?.Debug("OAuth2 token cache invalidated for clientId={ClientId}", config.ClientId);
    }

    private async Task<string> FetchTokenAsync(OAuth2Config config, CancellationToken ct)
    {
        HttpClient client = _httpClientFactory.CreateClient("OAuth2Auth");

        // Build form body for client_credentials grant
        var formParams = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret
        };

        if (!string.IsNullOrWhiteSpace(config.Scope))
            formParams["scope"] = config.Scope;

        if (!string.IsNullOrWhiteSpace(config.Audience))
            formParams["audience"] = config.Audience;

        using var request = new HttpRequestMessage(HttpMethod.Post, config.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(formParams)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct);
        using JsonDocument doc = JsonDocument.Parse(json);

        string accessToken = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new MConfigurationException("OAuth2 token response missing 'access_token'", "access_token");

        // Parse expires_in (default to 3600s if missing)
        int expiresIn = doc.RootElement.TryGetProperty("expires_in", out JsonElement expiresElem)
            ? expiresElem.GetInt32()
            : 3600;

        // Cache with 60-second early refresh buffer
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);
        string cacheKey = BuildCacheKey(config);
        _cache[cacheKey] = (accessToken, expiresAt);

        _logger?.Debug("OAuth2 token fetched for clientId={ClientId}, expires in {ExpiresIn}s", config.ClientId, expiresIn);

        return accessToken;
    }

    private static string BuildCacheKey(OAuth2Config config) =>
        $"{config.ClientId}:{config.TokenEndpoint}";
}
