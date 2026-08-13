using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Integration.Connectors.Http;

namespace Muonroi.Integration.Tests;

/// <summary>
/// Unit tests for UrlSafetyValidator covering all SSRF attack vectors:
/// loopback, private IP ranges, link-local, AWS metadata, DNS rebinding, and non-http schemes.
/// </summary>
public class UrlSafetyValidatorTests
{
    // ─── Null / invalid input ───────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_NullUrl_Throws()
    {
        await Assert.ThrowsAsync<MInternalException>(() => UrlSafetyValidator.ValidateAsync(null));
    }

    [Fact]
    public async Task ValidateAsync_EmptyUrl_Throws()
    {
        await Assert.ThrowsAsync<MInternalException>(() => UrlSafetyValidator.ValidateAsync(""));
    }

    [Fact]
    public async Task ValidateAsync_NotAUrl_Throws()
    {
        await Assert.ThrowsAsync<MArgumentException>(() => UrlSafetyValidator.ValidateAsync("not-a-url"));
    }

    // ─── Non-http schemes ───────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_FtpScheme_Throws()
    {
        await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync("ftp://example.com/file"));
    }

    [Fact]
    public async Task ValidateAsync_FileScheme_Throws()
    {
        await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync("file:///etc/passwd"));
    }

    // ─── Loopback / localhost ────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_Loopback127_Throws()
    {
        MInternalException ex = await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync("http://127.0.0.1/secret"));
        Assert.Equal("CONNECTOR:BLOCKED_URL", ex.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_LocalhostDns_Throws()
    {
        // DNS resolves "localhost" → 127.0.0.1 (DNS rebinding protection)
        await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync("http://localhost/secret"));
    }

    // ─── Private IP ranges ───────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_PrivateClass_A_10_Throws()
    {
        await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync("http://10.0.0.5/admin"));
    }

    [Fact]
    public async Task ValidateAsync_PrivateClass_A_10_HighOctet_Throws()
    {
        await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync("http://10.255.255.255/admin"));
    }

    [Fact]
    public async Task ValidateAsync_PrivateClass_B_172_16_Throws()
    {
        await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync("http://172.16.0.1/internal"));
    }

    [Fact]
    public async Task ValidateAsync_PrivateClass_B_172_31_Throws()
    {
        await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync("http://172.31.255.255/internal"));
    }

    [Fact]
    public async Task ValidateAsync_PrivateClass_C_192_168_Throws()
    {
        await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync("http://192.168.1.1/api"));
    }

    // ─── Link-local and cloud metadata ───────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_LinkLocal_169_254_Throws()
    {
        await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync("http://169.254.0.1/"));
    }

    [Fact]
    public async Task ValidateAsync_AwsMetadata_Throws()
    {
        MInternalException ex = await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync("http://169.254.169.254/latest/meta-data"));
        Assert.Equal("CONNECTOR:BLOCKED_URL", ex.ErrorCode);
    }

    // ─── IPv6 blocked ranges ──────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_Ipv6Loopback_Throws()
    {
        await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync("http://[::1]/secret"));
    }

    [Fact]
    public async Task ValidateAsync_Ipv6LinkLocal_Throws()
    {
        await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync("http://[fe80::1]/secret"));
    }

    // ─── Legitimate external URLs ─────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_ExternalHttps_DoesNotThrow()
    {
        // Use a literal public IP (8.8.8.8 — Google DNS) to avoid DNS dependency in tests.
        // This verifies that non-blocked IPs pass validation.
        await UrlSafetyValidator.ValidateAsync("https://8.8.8.8/");
    }

    [Fact]
    public async Task ValidateAsync_ExternalHttp_DoesNotThrow()
    {
        // Use a literal public IP to verify http scheme is allowed for non-blocked addresses.
        await UrlSafetyValidator.ValidateAsync("http://8.8.8.8/");
    }

    // ─── Error code verification ───────────────────────────────────────────────

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://10.0.0.1/")]
    [InlineData("http://172.16.0.1/")]
    [InlineData("http://192.168.0.1/")]
    [InlineData("http://169.254.169.254/")]
    public async Task ValidateAsync_BlockedUrls_HaveCorrectErrorCode(string url)
    {
        MInternalException ex = await Assert.ThrowsAsync<MInternalException>(() =>
            UrlSafetyValidator.ValidateAsync(url));
        Assert.Equal("CONNECTOR:BLOCKED_URL", ex.ErrorCode);
    }
}
