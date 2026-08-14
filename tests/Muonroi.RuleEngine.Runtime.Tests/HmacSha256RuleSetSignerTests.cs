namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class HmacSha256RuleSetSignerTests
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes("test-secret-key-12345");

    [Fact]
    public void Sign_ReturnsNonEmptyBase64String()
    {
        HmacSha256RuleSetSigner sut = new(_key);

        string signature = sut.Sign("hello world");

        signature.Should().NotBeNullOrWhiteSpace();
        Convert.FromBase64String(signature).Should().NotBeEmpty();
    }

    [Fact]
    public void Sign_SameContent_ReturnsSameSignature()
    {
        HmacSha256RuleSetSigner sut = new(_key);

        string sig1 = sut.Sign("hello");
        string sig2 = sut.Sign("hello");

        sig1.Should().Be(sig2);
    }

    [Fact]
    public void Sign_DifferentContent_ReturnsDifferentSignature()
    {
        HmacSha256RuleSetSigner sut = new(_key);

        string sig1 = sut.Sign("hello");
        string sig2 = sut.Sign("world");

        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        HmacSha256RuleSetSigner sut = new(_key);
        string content = "test content";
        string signature = sut.Sign(content);

        bool result = sut.Verify(content, signature);

        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_InvalidSignature_ReturnsFalse()
    {
        HmacSha256RuleSetSigner sut = new(_key);
        string content = "test content";
        string signature = sut.Sign(content);

        bool result = sut.Verify("tampered content", signature);

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_MalformedBase64_ReturnsFalse()
    {
        HmacSha256RuleSetSigner sut = new(_key);

        bool result = sut.Verify("content", "not-valid-base64!!!");

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_DifferentKeys_ReturnsFalse()
    {
        HmacSha256RuleSetSigner signer1 = new(Encoding.UTF8.GetBytes("key-1"));
        HmacSha256RuleSetSigner signer2 = new(Encoding.UTF8.GetBytes("key-2"));

        string signature = signer1.Sign("content");
        bool result = signer2.Verify("content", signature);

        result.Should().BeFalse();
    }
}
