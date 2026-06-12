namespace Muonroi.Auth.Tests;

using Muonroi.Auth.Mfa.WebAuthenticate;
using Muonroi.Core.Abstractions.Interfaces;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Caching.Distributed;

public class WebAuthnTests
{
    private readonly IWebAuthnCredentialStore _credentialStore = Substitute.For<IWebAuthnCredentialStore>();
    private readonly IFido2 _fido2 = Substitute.For<IFido2>();
    private readonly IDistributedCache _cache = Substitute.For<IDistributedCache>();
    private readonly IMJsonSerializeService _jsonService = Substitute.For<IMJsonSerializeService>();
    private readonly IMDateTimeService _dateTimeService = Substitute.For<IMDateTimeService>();
    private readonly WebAuthenticateService _service;

    public WebAuthnTests()
    {
        _credentialStore.GetCredentialIdsByUserAsync(Arg.Any<string?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<byte[]>());
        _credentialStore.GetCredentialsByUserAsync(Arg.Any<string?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<WebAuthnCredentialInfo>());
        _credentialStore.GetAllCredentialIdsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<byte[]>());

        _service = new WebAuthenticateService(_fido2, _cache, _credentialStore, _jsonService, _dateTimeService);
    }

    [Fact]
    public void AddWebAuthn_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebAuthn:ServerDomain"] = "localhost",
                ["WebAuthn:ServerName"] = "Test"
            })
            .Build();

        // Act
        services.AddWebAuthn(configuration);

        // Assert
        services.Any(d => d.ServiceType == typeof(WebAuthenticateService)).Should().BeTrue();
    }

    [Fact]
    public async Task BeginRegistrationAsync_ShouldReturnOptions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var options = new CredentialCreateOptions
        {
            Rp = new PublicKeyCredentialRpEntity("id", "name"),
            User = new Fido2User { Id = [1], Name = "name", DisplayName = "display" },
            Challenge = [1],
            PubKeyCredParams = []
        };
        _fido2.RequestNewCredential(Arg.Any<RequestNewCredentialParams>())
            .Returns(options);

        // Act
        var result = await _service.BeginRegistrationAsync(userId, "user", "display", CancellationToken.None);

        // Assert
        result.Should().BeSameAs(options);
        await _cache.Received().SetAsync(Arg.Is<string>(s => s.StartsWith("webauthn:reg:")), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BeginAuthenticationAsync_ShouldReturnOptions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var options = new AssertionOptions
        {
            Challenge = [1]
        };
        _fido2.GetAssertionOptions(Arg.Any<GetAssertionOptionsParams>())
            .Returns(options);

        // Act
        var result = await _service.BeginAuthenticationAsync(userId, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(options);
        await _cache.Received().SetAsync(Arg.Is<string>(s => s.StartsWith("webauthn:auth:")), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>());
    }
}
