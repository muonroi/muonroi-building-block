using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Auth;

/// <summary>
/// Switch-based resolver that dispatches to the correct auth strategy based on ExternalProjectConfig.AuthStrategy.
///
/// Strategy behavior:
/// - None: empty headers, no custom client
/// - StaticHeaders: pass through ExecutionHeaders, applying API key rotation if configured
/// - OAuth2ClientCredentials: fetch/cache bearer token via IOAuth2TokenProvider
/// - MutualTls: create mTLS HttpClient via IMtlsHttpClientFactory
/// </summary>
/// <remarks>Creates a resolver for external project auth strategies.</remarks>
public sealed class AuthStrategyResolver(
    IOAuth2TokenProvider oauth2Provider,
    IMtlsHttpClientFactory mtlsFactory,
    IApiKeyRotationService apiKeyRotation) : IAuthStrategyResolver
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>();

    private readonly IOAuth2TokenProvider _oauth2Provider = oauth2Provider;
    private readonly IMtlsHttpClientFactory _mtlsFactory = mtlsFactory;
    private readonly IApiKeyRotationService _apiKeyRotation = apiKeyRotation;

    /// <inheritdoc/>
    public async Task<AuthResult> ResolveAsync(ExternalProjectConfig config, CancellationToken ct)
    {
        return config.AuthStrategy switch
        {
            AuthStrategy.None => new AuthResult
            {
                Headers = EmptyHeaders,
                CustomHttpClient = null
            },

            AuthStrategy.StaticHeaders => await ResolveStaticHeadersAsync(config, ct),

            AuthStrategy.OAuth2ClientCredentials => await ResolveOAuth2Async(config, ct),

            AuthStrategy.MutualTls => ResolveMutualTls(config),

            _ => new AuthResult { Headers = EmptyHeaders, CustomHttpClient = null }
        };
    }

    private async Task<AuthResult> ResolveStaticHeadersAsync(ExternalProjectConfig config, CancellationToken ct)
    {
        // Check and rotate API key if configured, otherwise return headers as-is
        IReadOnlyDictionary<string, string> headers = await _apiKeyRotation.CheckAndRotateAsync(config, ct);

        return new AuthResult
        {
            Headers = headers,
            CustomHttpClient = null
        };
    }

    private async Task<AuthResult> ResolveOAuth2Async(ExternalProjectConfig config, CancellationToken ct)
    {
        OAuth2Config oauth2 = MGuard.Configured(config.OAuth2, 
            $"ExternalProjectConfig.OAuth2 is required when AuthStrategy is OAuth2ClientCredentials (ProjectId={config.ProjectId})", "OAuth2");

        string accessToken = await _oauth2Provider.GetAccessTokenAsync(oauth2, ct);

        return new AuthResult
        {
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {accessToken}"
            },
            CustomHttpClient = null
        };
    }

    private AuthResult ResolveMutualTls(ExternalProjectConfig config)
    {
        MtlsConfig mtls = MGuard.Configured(config.Mtls, 
            $"ExternalProjectConfig.Mtls is required when AuthStrategy is MutualTls (ProjectId={config.ProjectId})", "Mtls");

        HttpClient customClient = _mtlsFactory.CreateClient(config.ProjectId, mtls);

        return new AuthResult
        {
            Headers = EmptyHeaders,
            CustomHttpClient = customClient
        };
    }
}
