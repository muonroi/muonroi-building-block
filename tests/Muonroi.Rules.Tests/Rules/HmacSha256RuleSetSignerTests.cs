using System.Security.Cryptography;

namespace Muonroi.Rules.Tests.Rules;

public class HmacSha256RuleSetSignerTests
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Sign_ShouldReturnBase64String()
    {
        var signer = new HmacSha256RuleSetSigner(_key);

        string signature = signer.Sign("test content");

        signature.Should().NotBeNullOrWhiteSpace();
        // Should be valid base64
        Action act = () => Convert.FromBase64String(signature);
        act.Should().NotThrow();
    }

    [Fact]
    public void Sign_SameContent_ShouldReturnSameSignature()
    {
        var signer = new HmacSha256RuleSetSigner(_key);

        string sig1 = signer.Sign("hello");
        string sig2 = signer.Sign("hello");

        sig1.Should().Be(sig2);
    }

    [Fact]
    public void Sign_DifferentContent_ShouldReturnDifferentSignatures()
    {
        var signer = new HmacSha256RuleSetSigner(_key);

        string sig1 = signer.Sign("content A");
        string sig2 = signer.Sign("content B");

        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void Verify_ValidSignature_ShouldReturnTrue()
    {
        var signer = new HmacSha256RuleSetSigner(_key);
        string content = "test payload";
        string signature = signer.Sign(content);

        signer.Verify(content, signature).Should().BeTrue();
    }

    [Fact]
    public void Verify_TamperedContent_ShouldReturnFalse()
    {
        var signer = new HmacSha256RuleSetSigner(_key);
        string signature = signer.Sign("original");

        signer.Verify("tampered", signature).Should().BeFalse();
    }

    [Fact]
    public void Verify_InvalidBase64Signature_ShouldReturnFalse()
    {
        var signer = new HmacSha256RuleSetSigner(_key);

        signer.Verify("content", "not-valid-base64!!!").Should().BeFalse();
    }

    [Fact]
    public void Verify_DifferentKey_ShouldReturnFalse()
    {
        var signer1 = new HmacSha256RuleSetSigner(RandomNumberGenerator.GetBytes(32));
        var signer2 = new HmacSha256RuleSetSigner(RandomNumberGenerator.GetBytes(32));

        string signature = signer1.Sign("data");

        signer2.Verify("data", signature).Should().BeFalse();
    }

    [Fact]
    public void Sign_EmptyContent_ShouldNotThrow()
    {
        var signer = new HmacSha256RuleSetSigner(_key);

        string signature = signer.Sign("");

        signature.Should().NotBeNullOrWhiteSpace();
        signer.Verify("", signature).Should().BeTrue();
    }
}
