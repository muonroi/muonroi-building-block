using System.Net;
using Microsoft.Extensions.Options;
using Muonroi.RuleEngine.Core.Events;

namespace Muonroi.RuleEngine.Core.Tests.Events;

/// <summary>
/// Tests for HttpRuleWebhookNotifier — HTTP POST delivery with exponential backoff retry.
/// Uses a custom MockHttpMessageHandler to simulate server responses without real HTTP calls.
/// </summary>
public sealed class HttpRuleWebhookNotifierTests
{
    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Creates a WebhookOptions instance with a short initial delay suitable for unit tests.
    /// </summary>
    private static WebhookOptions FastOptions(string url = "https://example.com/webhook", int maxRetries = 3)
        => new()
        {
            Url = url,
            MaxRetries = maxRetries,
            InitialRetryDelay = TimeSpan.FromMilliseconds(1), // avoid slow tests
            Timeout = TimeSpan.FromSeconds(5)
        };

    /// <summary>
    /// Builds a notifier backed by the supplied mock handler.
    /// </summary>
    private static HttpRuleWebhookNotifier Build(MockHttpMessageHandler handler, WebhookOptions? opts = null)
    {
        opts ??= FastOptions();
        var factory = new SingleClientFactory(handler);
        var options = Options.Create(opts);
        return new HttpRuleWebhookNotifier(factory, options);
    }

    private static CloudEvent MakeEvent() => new()
    {
        Type = RuleCloudEventTypes.RuleSetChanged,
        Source = "/test"
    };

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task NotifyAsync_Success_ReturnsSuccessResultAndSendsPost()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var notifier = Build(handler);

        // Act
        var result = await notifier.NotifyAsync(MakeEvent());

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Attempts);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task NotifyAsync_PostsWithCloudEventsContentType()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var notifier = Build(handler);

        // Act
        await notifier.NotifyAsync(MakeEvent());

        // Assert — Content-Type must be application/cloudevents+json
        Assert.NotNull(handler.LastRequest);
        Assert.NotNull(handler.LastRequest!.Content);
        var contentType = handler.LastRequest.Content!.Headers.ContentType?.MediaType;
        Assert.Equal("application/cloudevents+json", contentType);
    }

    [Fact]
    public async Task NotifyAsync_PostsToConfiguredUrl()
    {
        // Arrange
        const string url = "https://hooks.example.com/rule-events";
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var notifier = Build(handler, FastOptions(url: url));

        // Act
        await notifier.NotifyAsync(MakeEvent());

        // Assert
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(url, handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task NotifyAsync_RetriesOnServerError_ThenSucceeds()
    {
        // Arrange — first call returns 500, second returns 200
        var handler = new MockHttpMessageHandler(
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK);
        var notifier = Build(handler);

        // Act
        var result = await notifier.NotifyAsync(MakeEvent());

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Attempts);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task NotifyAsync_ExponentialBackoff_DelayDoublesEachAttempt()
    {
        // Arrange — three 500s then success
        var handler = new MockHttpMessageHandler(
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK);
        var delays = new List<TimeSpan>();
        var opts = new WebhookOptions
        {
            Url = "https://example.com/hook",
            MaxRetries = 4,
            InitialRetryDelay = TimeSpan.FromMilliseconds(10)
        };
        var notifier = new HttpRuleWebhookNotifier(
            new SingleClientFactory(handler),
            Options.Create(opts));

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await notifier.NotifyAsync(MakeEvent());
        sw.Stop();

        // Assert — 4 attempts, success on 4th
        Assert.True(result.Success);
        Assert.Equal(4, result.Attempts);

        // Exponential delays: 10ms * 2^0=10, 10ms * 2^1=20, 10ms * 2^2=40 → total >= 70ms
        Assert.True(sw.ElapsedMilliseconds >= 60,
            $"Expected at least 60ms elapsed for exponential backoff, got {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task NotifyAsync_DoesNotRetryOnClientError_4xx()
    {
        // Arrange — 400 Bad Request should NOT retry
        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest);
        var notifier = Build(handler);

        // Act
        var result = await notifier.NotifyAsync(MakeEvent());

        // Assert
        Assert.False(result.Success);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, handler.CallCount); // only 1 call, no retries
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task NotifyAsync_DoesNotRetryOnOther4xx()
    {
        // Arrange — 403 Forbidden also should NOT retry
        var handler = new MockHttpMessageHandler(HttpStatusCode.Forbidden);
        var notifier = Build(handler);

        // Act
        var result = await notifier.NotifyAsync(MakeEvent());

        // Assert
        Assert.False(result.Success);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task NotifyAsync_RetriesOnHttpRequestException()
    {
        // Arrange — network failure then success
        var handler = new MockHttpMessageHandler(throwOnFirst: true, HttpStatusCode.OK);
        var notifier = Build(handler);

        // Act
        var result = await notifier.NotifyAsync(MakeEvent());

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Attempts);
    }

    [Fact]
    public async Task NotifyAsync_RespectsMaxRetries_ReturnsFailureAfterExhaustion()
    {
        // Arrange — always 500, MaxRetries=2 → total 3 attempts (0, 1, 2)
        var handler = new MockHttpMessageHandler(
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError);
        var notifier = Build(handler, FastOptions(maxRetries: 2));

        // Act
        var result = await notifier.NotifyAsync(MakeEvent());

        // Assert
        Assert.False(result.Success);
        Assert.Equal(3, handler.CallCount); // initial + 2 retries
    }

    [Fact]
    public async Task NotifyAsync_CancellationDuringRetryWait_ReturnsCancelled()
    {
        // Arrange — 500 so it will try to retry with delay
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError);
        var opts = new WebhookOptions
        {
            Url = "https://example.com/hook",
            MaxRetries = 3,
            InitialRetryDelay = TimeSpan.FromSeconds(10) // long delay so we can cancel it
        };
        var notifier = new HttpRuleWebhookNotifier(
            new SingleClientFactory(handler),
            Options.Create(opts));

        using var cts = new CancellationTokenSource();

        // Cancel very quickly
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        // Act
        var result = await notifier.NotifyAsync(MakeEvent(), cts.Token);

        // Assert — was cancelled
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task WebhookOptions_DefaultUrlEmpty_ReturnsFailure()
    {
        // Arrange — empty URL means not configured
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var opts = new WebhookOptions { Url = string.Empty };
        var notifier = new HttpRuleWebhookNotifier(
            new SingleClientFactory(handler),
            Options.Create(opts));

        // Act
        var result = await notifier.NotifyAsync(MakeEvent());

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, handler.CallCount); // no HTTP call made
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task NotifyAsync_Returns_WebhookResult_WithAttemptCount()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var notifier = Build(handler);

        // Act
        var result = await notifier.NotifyAsync(MakeEvent());

        // Assert — result is a proper record
        Assert.IsType<WebhookResult>(result);
        Assert.Equal(1, result.Attempts);
        Assert.True(result.Success);
    }
}

// ------------------------------------------------------------------ mock infrastructure

/// <summary>
/// A minimal DelegatingHandler that returns pre-configured responses in sequence.
/// Supports simulating network failures on the first call.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();
    private int _callCount;

    public int CallCount => _callCount;
    public HttpRequestMessage? LastRequest { get; private set; }

    public MockHttpMessageHandler(params HttpStatusCode[] statuses)
    {
        foreach (var code in statuses)
            _responses.Enqueue(() => new HttpResponseMessage(code));
    }

    /// <summary>
    /// Constructs a handler that throws HttpRequestException on the first call, then returns the given statuses.
    /// </summary>
    public MockHttpMessageHandler(bool throwOnFirst, params HttpStatusCode[] statuses)
    {
        if (throwOnFirst)
            _responses.Enqueue(() => throw new HttpRequestException("Simulated network failure"));
        foreach (var code in statuses)
            _responses.Enqueue(() => new HttpResponseMessage(code));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        LastRequest = request;

        if (_responses.TryDequeue(out var factory))
            return Task.FromResult(factory());

        // Default: keep returning 500 when no more responses are queued
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }
}

/// <summary>
/// A minimal IHttpClientFactory that creates an HttpClient backed by the given handler.
/// Uses a named client "RuleWebhook" as expected by HttpRuleWebhookNotifier.
/// </summary>
internal sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler = handler;

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}
