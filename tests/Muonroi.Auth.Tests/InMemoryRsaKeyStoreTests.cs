using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Muonroi.Auth.Jwt;
using Xunit;

namespace Muonroi.Auth.Tests;

public class InMemoryRsaKeyStoreTests
{
    [Fact]
    public void Constructor_InitializesWithOneKey()
    {
        var store = new InMemoryRsaKeyStore();

        var creds = store.GetCurrentSigningCredentials();

        creds.Should().NotBeNull();
        creds.Algorithm.Should().Be(SecurityAlgorithms.RsaSha256);
    }

    [Fact]
    public void RotateKeys_CreatesNewActiveKey()
    {
        var store = new InMemoryRsaKeyStore();
        var creds1 = store.GetCurrentSigningCredentials();

        store.RotateKeys();
        var creds2 = store.GetCurrentSigningCredentials();

        creds2.Kid.Should().NotBe(creds1.Kid);
    }

    [Fact]
    public void GetKey_ExistingKid_ReturnsKey()
    {
        var store = new InMemoryRsaKeyStore();
        var creds = store.GetCurrentSigningCredentials();

        var key = store.GetKey(creds.Kid);

        key.Should().NotBeNull();
    }

    [Fact]
    public void GetKey_NonExistentKid_ReturnsNull()
    {
        var store = new InMemoryRsaKeyStore();

        var key = store.GetKey("non-existent-kid");

        key.Should().BeNull();
    }

    [Fact]
    public void RotateKeys_KeepsMaxTwoKeys()
    {
        var store = new InMemoryRsaKeyStore();

        // Initial key + 3 rotations = 4 total, but should trim to 2
        store.RotateKeys();
        store.RotateKeys();
        store.RotateKeys();

        var jwks = store.GetJsonWebKeySet();
        jwks.Keys.Count.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void GetJsonWebKeySet_ReturnsPublicKeys()
    {
        var store = new InMemoryRsaKeyStore();

        var jwks = store.GetJsonWebKeySet();

        jwks.Keys.Should().NotBeEmpty();
        jwks.Keys[0].Use.Should().Be("sig");
        jwks.Keys[0].Alg.Should().Be(SecurityAlgorithms.RsaSha256);
    }
}
