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

        request.Url.Should().Contain("response_type=code");
        request.Url.Should().Contain("code_challenge=");
        request.Url.Should().Contain("code_challenge_method=S256");
        request.Url.Should().Contain($"redirect_uri={Uri.EscapeDataString(options.RedirectUri)}");
        request.Url.Should().Contain("state=");
        request.Url.Should().Contain("nonce=");
        request.CodeVerifier.Should().NotBeNullOrWhiteSpace();
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

        Func<Task> action = () => client.RedeemCodeForTokenAsync("code", "verifier", "https://wrong", new HttpClient(new StubHandler()));

        await action.Should().ThrowAsync<InvalidOperationException>();
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

        TokenResponse token = await client.RefreshTokenAsync("old", new HttpClient(new StubHandler("{\"access_token\":\"a2\",\"refresh_token\":\"new\"}")));

        token.RefreshToken.Should().Be("new");
        token.RefreshToken.Should().NotBe("old");
    }

    private sealed class StubHandler(string json = "{\"access_token\":\"a\",\"refresh_token\":\"r\"}") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
