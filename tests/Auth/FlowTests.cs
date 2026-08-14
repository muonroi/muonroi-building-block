namespace Muonroi.Auth.Tests;

public class FlowTests
{
    [Fact]
    public void CreateAuthorizationRequest_BuildsPkceUrl()
    {
        OidcOptions options = new()
        {
            Authority = "https://auth.example",
            ClientId = "client",
            RedirectUri = "myapp://callback",
            Scopes = ["openid", "profile"]
        };
        PkceClient client = new(options);
        AuthorizationRequest request = client.CreateAuthorizationRequest();

        Assert.Contains("response_type=code", request.Url);
        Assert.Contains("code_challenge=", request.Url);
        Assert.Contains("code_challenge_method=S256", request.Url);
        Assert.Contains($"redirect_uri={Uri.EscapeDataString(options.RedirectUri)}", request.Url);
        Assert.Contains("state=", request.Url);
        Assert.Contains("nonce=", request.Url);
        Assert.NotNull(request.CodeVerifier);
    }

    [Fact]
    public async Task RedeemCodeForToken_RequiresExactRedirect()
    {
        OidcOptions options = new()
        {
            Authority = "https://auth.example",
            ClientId = "client",
            RedirectUri = "myapp://callback",
            Scopes = ["openid"]
        };
        PkceClient client = new(options);
        // MGuard.Against throws MArgumentException (redirect URI is a method argument).
        await Assert.ThrowsAsync<MArgumentException>(() =>
            client.RedeemCodeForTokenAsync("code", "verifier", "https://wrong", new HttpClient(new StubHandler())));
    }

    [Fact]
    public async Task RefreshToken_RotatesValue()
    {
        OidcOptions options = new()
        {
            Authority = "https://auth.example",
            ClientId = "client",
            RedirectUri = "myapp://callback",
            Scopes = ["openid"]
        };
        PkceClient client = new(options);
        string oldToken = "old";
        StubHandler handler = new("{\"access_token\":\"a2\",\"refresh_token\":\"new\"}");
        TokenResponse token = await client.RefreshTokenAsync(oldToken, new HttpClient(handler));
        Assert.Equal("new", token.RefreshToken);
        Assert.NotEqual(oldToken, token.RefreshToken);
    }

    private class StubHandler(string json = "{\"access_token\":\"a\",\"refresh_token\":\"r\"}")
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}