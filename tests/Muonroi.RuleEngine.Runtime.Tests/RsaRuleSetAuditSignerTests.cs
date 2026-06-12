using Muonroi.Core.Abstractions.Exceptions;
using System.Security.Cryptography;
using FluentAssertions;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RsaRuleSetAuditSignerTests
{
    [Fact]
    public void Sign_ReturnsNonEmptyBase64()
    {
        using RsaRuleSetAuditSigner sut = RsaRuleSetAuditSigner.CreateEphemeral();

        string sig = sut.Sign("test payload");

        sig.Should().NotBeNullOrWhiteSpace();
        Convert.FromBase64String(sig).Should().NotBeEmpty();
    }

    [Fact]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        using RsaRuleSetAuditSigner sut = RsaRuleSetAuditSigner.CreateEphemeral();
        string payload = "test payload";
        string sig = sut.Sign(payload);

        bool result = sut.Verify(payload, sig);

        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_TamperedPayload_ReturnsFalse()
    {
        using RsaRuleSetAuditSigner sut = RsaRuleSetAuditSigner.CreateEphemeral();
        string sig = sut.Sign("original");

        bool result = sut.Verify("tampered", sig);

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_EmptyPayload_ReturnsFalse()
    {
        using RsaRuleSetAuditSigner sut = RsaRuleSetAuditSigner.CreateEphemeral();

        sut.Verify("", "sig").Should().BeFalse();
        sut.Verify(null!, "sig").Should().BeFalse();
        sut.Verify("payload", "").Should().BeFalse();
    }

    [Fact]
    public void Verify_InvalidBase64Signature_ReturnsFalse()
    {
        using RsaRuleSetAuditSigner sut = RsaRuleSetAuditSigner.CreateEphemeral();

        bool result = sut.Verify("payload", "not-base64!!!");

        result.Should().BeFalse();
    }

    [Fact]
    public void KeyId_DefaultValue()
    {
        using RsaRuleSetAuditSigner sut = RsaRuleSetAuditSigner.CreateEphemeral();

        sut.KeyId.Should().Be("ruleset-control-plane-ephemeral");
    }

    [Fact]
    public void KeyId_CustomValue()
    {
        using RSA rsa = RSA.Create(2048);
        using RsaRuleSetAuditSigner sut = new(rsa, "my-key");

        sut.KeyId.Should().Be("my-key");
    }

    [Fact]
    public void SignatureAlgorithm_IsRsaSha256()
    {
        using RsaRuleSetAuditSigner sut = RsaRuleSetAuditSigner.CreateEphemeral();

        sut.SignatureAlgorithm.Should().Be("RSA-SHA256");
    }

    [Fact]
    public void Sign_NullPayload_Throws()
    {
        using RsaRuleSetAuditSigner sut = RsaRuleSetAuditSigner.CreateEphemeral();

        Action act = () => sut.Sign(null!);

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void FromPrivateKeyFile_NonExistentFile_Throws()
    {
        Action act = () => RsaRuleSetAuditSigner.FromPrivateKeyFile("/nonexistent/path.pem");

        act.Should().Throw<MConfigurationException>();
    }

    [Fact]
    public void FromPrivateKeyPem_EmptyPem_Throws()
    {
        Action act = () => RsaRuleSetAuditSigner.FromPrivateKeyPem("");

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Constructor_NullRsa_Throws()
    {
        Action act = () => new RsaRuleSetAuditSigner(null!);

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Verify_DifferentKeys_ReturnsFalse()
    {
        using RsaRuleSetAuditSigner signer1 = RsaRuleSetAuditSigner.CreateEphemeral();
        using RsaRuleSetAuditSigner signer2 = RsaRuleSetAuditSigner.CreateEphemeral();

        string sig = signer1.Sign("payload");
        bool result = signer2.Verify("payload", sig);

        result.Should().BeFalse();
    }
}
