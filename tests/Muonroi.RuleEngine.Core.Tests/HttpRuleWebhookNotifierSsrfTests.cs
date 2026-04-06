using System.Net;
using Microsoft.Extensions.Options;
using Muonroi.RuleEngine.Core.Events;

namespace Muonroi.RuleEngine.Core.Tests.Events;

/// <summary>
/// SSRF protection tests for HttpRuleWebhookNotifier.
/// Verifies that webhook URLs pointing to internal/blocked addresses are rejected
/// before any HTTP connection is attempted.
/// </summary>
public sealed class HttpRuleWebhookNotifierSsrfTests
{
    private static WebhookOptions OptionsWithUrl(string url) => new()
    {
        Url = url,
        MaxRetries = 0,
        InitialRetryDelay = TimeSpan.FromMilliseconds(1),
        Timeout = TimeSpan.FromSeconds(5)
    };

    private static HttpRuleWebhookNotifier BuildNotifier(string url, MockHttpMessageHandler? handler = null)
    {
        handler ??= new MockHttpMessageHandler(HttpStatusCode.OK);
        var factory = new SingleClientFactory(handler);
        var options = Options.Create(OptionsWithUrl(url));
        return new HttpRuleWebhookNotifier(factory, options);
    }

    private static CloudEvent MakeEvent() => new()
    {
        Type = RuleCloudEventTypes.RuleSetChanged,
        Source = "/ssrf-test"
    };

    // ─── Blocked URLs ────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyAsync_AwsMetadataUrl_ReturnsFail_WithoutHttpCall()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var notifier = BuildNotifier("http://169.254.169.254/latest", handler);

        // Act
        WebhookResult result = await notifier.NotifyAsync(MakeEvent());

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(0, handler.CallCount); // no HTTP call should be made
    }

    [Fact]
    public async Task NotifyAsync_LoopbackUrl_ReturnsFail_WithoutHttpCall()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var notifier = BuildNotifier("http://127.0.0.1/admin", handler);

        // Act
        WebhookResult result = await notifier.NotifyAsync(MakeEvent());

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task NotifyAsync_PrivateClassAUrl_ReturnsFail_WithoutHttpCall()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var notifier = BuildNotifier("http://10.0.0.5/internal-api", handler);

        // Act
        WebhookResult result = await notifier.NotifyAsync(MakeEvent());

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, handler.CallCount);
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://10.0.0.1/hook")]
    [InlineData("http://172.16.0.1/hook")]
    [InlineData("http://192.168.0.1/hook")]
    [InlineData("http://169.254.169.254/latest")]
    [InlineData("http://[::1]/hook")]
    public async Task NotifyAsync_BlockedUrls_AllReturnFailWithZeroAttempts(string url)
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var notifier = BuildNotifier(url, handler);

        // Act
        WebhookResult result = await notifier.NotifyAsync(MakeEvent());

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, handler.CallCount);
    }

    // ─── Valid external URLs ──────────────────────────────────────────────────

    [Fact]
    public async Task NotifyAsync_ValidExternalUrl_AttemptsHttpCall()
    {
        // Use literal public IP (8.8.8.8) to avoid DNS dependency in tests.
        // The mock handler returns 200 — we just verify an HTTP call was attempted.
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var notifier = BuildNotifier("https://8.8.8.8/webhook", handler);

        // Act
        WebhookResult result = await notifier.NotifyAsync(MakeEvent());

        // Assert — HTTP call was made (SSRF check passed)
        Assert.Equal(1, handler.CallCount);
        Assert.True(result.Success);
    }
}
