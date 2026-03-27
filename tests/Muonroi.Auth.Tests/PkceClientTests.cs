using FluentAssertions;
using Muonroi.Auth.Oidc;
using Xunit;

namespace Muonroi.Auth.Tests;

public class PkceClientTests
{
    private static OidcOptions CreateOptions() => new()
    {
        Authority = "https://auth.example.com",
        ClientId = "test-client",
        RedirectUri = "https://app.example.com/callback",
        Scopes = ["openid", "profile"]
    };

    [Fact]
    public void CreateAuthorizationRequest_ReturnsValidUrl()
    {
        var client = new PkceClient(CreateOptions());

        var request = client.CreateAuthorizationRequest();

        request.Url.Should().StartWith("https://auth.example.com/authorize?");
        request.Url.Should().Contain("response_type=code");
        request.Url.Should().Contain("client_id=test-client");
        request.Url.Should().Contain("code_challenge_method=S256");
        request.CodeVerifier.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CreateAuthorizationRequest_UniqueStateAndNonce()
    {
        var client = new PkceClient(CreateOptions());

        var req1 = client.CreateAuthorizationRequest();
        var req2 = client.CreateAuthorizationRequest();

        req1.Url.Should().NotBe(req2.Url, "state and nonce should be unique");
        req1.CodeVerifier.Should().NotBe(req2.CodeVerifier);
    }

    [Fact]
    public async Task RedeemCodeForTokenAsync_MismatchedRedirectUri_Throws()
    {
        var client = new PkceClient(CreateOptions());
        var httpClient = new HttpClient();

        Func<Task> act = () => client.RedeemCodeForTokenAsync(
            "code", "verifier", "https://wrong.example.com/callback", httpClient);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Redirect URI*");
    }

    [Fact]
    public void OidcOptions_Endpoints_DerivedFromAuthority()
    {
        var options = CreateOptions();

        options.AuthorizationEndpoint.Should().Be("https://auth.example.com/authorize");
        options.TokenEndpoint.Should().Be("https://auth.example.com/token");
    }

    [Fact]
    public void OidcOptions_AuthorityWithTrailingSlash_TrimsCorrectly()
    {
        var options = new OidcOptions { Authority = "https://auth.example.com/" };

        options.AuthorizationEndpoint.Should().Be("https://auth.example.com/authorize");
        options.TokenEndpoint.Should().Be("https://auth.example.com/token");
    }
}
