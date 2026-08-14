namespace Muonroi.Pdf.Tests.Enterprise;

/// <summary>
/// Tests for <see cref="HttpPdfTemplateRegistry"/> (REST Lookup/Resolve over a mock transport) and
/// <see cref="PdfTemplateHotReload"/> (polling version-change detection over a scripted registry).
/// </summary>
public sealed class PdfTemplateRegistryTests
{
    // ── HttpPdfTemplateRegistry ───────────────────────────────────────────────

    [Fact]
    public async Task Lookup_Returns_MappedDescriptor()
    {
        var registry = NewRegistry((_, _) =>
            Json(HttpStatusCode.OK, new HttpPdfTemplateRegistry.TemplateDescriptorDto("t1", "Invoice", "v3", ["a", "b"])));

        TemplateDescriptor? result = await registry.LookupAsync("t1");

        result.Should().NotBeNull();
        result!.TemplateId.Should().Be("t1");
        result.Name.Should().Be("Invoice");
        result.LatestVersion.Should().Be("v3");
        result.Tags.Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public async Task Lookup_NotFound_ReturnsNull()
    {
        var registry = NewRegistry((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));

        (await registry.LookupAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task Resolve_DecodesBase64Content()
    {
        byte[] payload = "<html>hi</html>"u8.ToArray();
        var registry = NewRegistry((_, _) => Json(HttpStatusCode.OK,
            new HttpPdfTemplateRegistry.TemplateVersionDto("t1", "v3", "text/html",
                Convert.ToBase64String(payload), DateTimeOffset.UnixEpoch)));

        TemplateVersion? result = await registry.ResolveAsync("t1", "latest");

        result.Should().NotBeNull();
        result!.Version.Should().Be("v3");
        result.ContentType.Should().Be("text/html");
        result.Content.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task Subscribe_Throws_NotSupported()
    {
        var registry = NewRegistry((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var act = async () => await registry.SubscribeAsync(new RecordingObserver());
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    // ── PdfTemplateHotReload ──────────────────────────────────────────────────

    [Fact]
    public async Task HotReload_ForwardsVersionChange()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5)); // CI safety net
        var registry = new SequencedRegistry(cts, "v1", "v2"); // seed v1, then change to v2
        var observer = new RecordingObserver();
        var hot = new PdfTemplateHotReload(registry, observer,
            new PdfTemplateHotReloadOptions { PollInterval = TimeSpan.Zero, TemplateIds = ["t1"] });

        await hot.StartAsync(cts.Token);

        observer.Changes.Should().ContainSingle();
        observer.Changes[0].ChangeKind.Should().Be(TemplateChangeKind.Updated);
        observer.Changes[0].NewVersion.Should().Be("v2");
        observer.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task HotReload_NoVersionChange_EmitsNoEvent()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        var registry = new SequencedRegistry(cts, "v1", "v1"); // unchanged across polls
        var observer = new RecordingObserver();
        var hot = new PdfTemplateHotReload(registry, observer,
            new PdfTemplateHotReloadOptions { PollInterval = TimeSpan.Zero, TemplateIds = ["t1"] });

        await hot.StartAsync(cts.Token);

        observer.Changes.Should().BeEmpty();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static HttpPdfTemplateRegistry NewRegistry(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
    {
        var client = new HttpClient(new MockHandler(handler)) { BaseAddress = new Uri("https://cp.local/") };
        return new HttpPdfTemplateRegistry(new StubHttpClientFactory(client));
    }

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T body) =>
        new(status) { Content = JsonContent.Create(body) };

    private sealed class MockHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request, cancellationToken));
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingObserver : IAsyncObserver<TemplateChange>
    {
        public List<TemplateChange> Changes { get; } = [];
        public bool Completed { get; private set; }

        public ValueTask OnNextAsync(TemplateChange value, CancellationToken cancellationToken = default)
        {
            Changes.Add(value);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnErrorAsync(Exception error, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask OnCompletedAsync(CancellationToken cancellationToken = default)
        {
            Completed = true;
            return ValueTask.CompletedTask;
        }
    }

    // Returns scripted latest-versions in order, then cancels the token once exhausted so the
    // polling loop terminates deterministically (no real timing dependence).
    private sealed class SequencedRegistry(CancellationTokenSource cts, params string?[] versions) : IMPdfTemplateRegistry
    {
        private readonly Queue<string?> _versions = new(versions);

        public Task<TemplateDescriptor?> LookupAsync(string templateId, CancellationToken cancellationToken = default)
        {
            string? version = _versions.Count > 0 ? _versions.Dequeue() : null;
            if (_versions.Count == 0)
                cts.Cancel();
            return Task.FromResult<TemplateDescriptor?>(
                version is null ? null : new TemplateDescriptor(templateId, "n", version, []));
        }

        public Task<TemplateVersion?> ResolveAsync(string templateId, string version, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IAsyncDisposable> SubscribeAsync(IAsyncObserver<TemplateChange> observer, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
