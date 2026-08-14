namespace Muonroi.Auth.Oidc;

/// <summary>
/// Minimal client implementing the Authorization Code flow with PKCE for SPA/native apps.
/// </summary>
public class PkceClient(OidcOptions options)
{
    // Cache JsonSerializerOptions instance to avoid CA1869
    private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Creates an authorization request and code verifier.
    /// </summary>
    public AuthorizationRequest CreateAuthorizationRequest()
    {
        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        string state = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        string nonce = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        Dictionary<string, string> query = new()
        {
            ["response_type"] = "code",
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.RedirectUri,
            ["scope"] = string.Join(" ", options.Scopes),
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
            ["nonce"] = nonce
        };

        string queryString = string.Join("&",
            query.Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));
        string url = $"{options.AuthorizationEndpoint}?{queryString}";
        return new AuthorizationRequest(url, codeVerifier);
    }

    /// <summary>
    /// Exchanges an authorization code for tokens.
    /// </summary>
    public async Task<TokenResponse> RedeemCodeForTokenAsync(string code, string codeVerifier, string redirectUri,
        HttpClient httpClient)
    {
        MGuard.Against(redirectUri != options.RedirectUri, "Redirect URI must exactly match configured redirect URI.");

        Dictionary<string, string> parameters = new()
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = options.ClientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        };

        HttpResponseMessage response = await httpClient.PostAsync(options.TokenEndpoint, new FormUrlEncodedContent(parameters));
        string json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenResponse>(json, CachedJsonSerializerOptions) ?? new TokenResponse(); // MBB002-exempt: requires custom JsonOptions (PropertyNameCaseInsensitive + SnakeCaseLower) not available in wrapper
    }

    /// <summary>
    /// Requests new tokens using a refresh token.
    /// </summary>
    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, HttpClient httpClient)
    {
        Dictionary<string, string> parameters = new()
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = options.ClientId,
            ["refresh_token"] = refreshToken
        };

        HttpResponseMessage response = await httpClient.PostAsync(options.TokenEndpoint, new FormUrlEncodedContent(parameters));
        string json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenResponse>(json, CachedJsonSerializerOptions) ?? new TokenResponse(); // MBB002-exempt: requires custom JsonOptions (PropertyNameCaseInsensitive + SnakeCaseLower) not available in wrapper
    }

    private static string GenerateCodeVerifier()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

/// <summary>
/// Information for initiating the authorization request.
/// </summary>
public record AuthorizationRequest
{
    /// <summary>
    /// Authorization endpoint URL with query string.
    /// </summary>
    public string Url { get; init; }
    /// <summary>
    /// PKCE code verifier value.
    /// </summary>
    public string CodeVerifier { get; init; }

    /// <summary>
    /// Initializes a new authorization request.
    /// </summary>
    public AuthorizationRequest(string url, string codeVerifier)
    {
        Url = url;
        CodeVerifier = codeVerifier;
    }
}

/// <summary>
/// Response returned by the token endpoint.
/// </summary>
public record TokenResponse
{
    /// <summary>
    /// Access token value.
    /// </summary>
    public string? AccessToken { get; init; }
    /// <summary>
    /// Refresh token value.
    /// </summary>
    public string? RefreshToken { get; init; }
    /// <summary>
    /// ID token value.
    /// </summary>
    public string? IdToken { get; init; }
}
