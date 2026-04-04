namespace Muonroi.Auth.Tests;

using System.Net.Http;
using System.Text.Json;
using Muonroi.Core.Abstractions.Exceptions;

public class PkceClientTests
{
    private readonly OidcOptions _options;
    private readonly PkceClient _client;

    public PkceClientTests()
    {
        _options = new OidcOptions
        {
            Authority = "https://auth.com",
            ClientId = "test-client",
            RedirectUri = "https://localhost/callback",
            Scopes = ["openid", "profile"]
        };
        _client = new PkceClient(_options);
    }

    [Fact]
    public void CreateAuthorizationRequest_ShouldReturnValidRequest()
    {
        // Act
        var request = _client.CreateAuthorizationRequest();

        // Assert
        request.Url.Should().Contain("client_id=test-client");
        request.Url.Should().Contain("response_type=code");
        request.Url.Should().Contain("code_challenge=");
        request.CodeVerifier.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RedeemCodeForTokenAsync_ShouldThrow_WhenRedirectUriMismatch()
    {
        // Act
        Func<Task> act = () => _client.RedeemCodeForTokenAsync("code", "verifier", "wrong-uri", new HttpClient());

        // Assert
        await act.Should().ThrowAsync<MInternalException>()
            .WithMessage("Redirect URI must exactly match configured redirect URI.");
    }
}
