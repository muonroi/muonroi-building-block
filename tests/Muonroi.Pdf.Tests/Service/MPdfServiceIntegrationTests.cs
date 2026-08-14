namespace Muonroi.Pdf.Tests.Service;

/// <summary>
/// End-to-end coverage through a real <c>AddPdf</c> container: SC2 (PDF-1.7 stream), SC3
/// (activity tags + page-count / operation metrics), and SC4 (render timeout cancellation).
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class MPdfServiceIntegrationTests
{
    // The box tree assigns inline text the default family "serif" (block-level font-family is not
    // inherited down to synthesized inline text nodes in the current cascade). We therefore declare
    // the embedded test face UNDER the "serif" family so the writer finds it — the headless
    // build host has no OS fonts, so an unembedded family would produce .notdef glyphs.
    private const string Html =
        "<html><head><style>@font-face{font-family:serif;src:url(test.ttf);}</style></head>" +
        "<body><p>Hello Muonroi PDF</p></body></html>";

    private static PdfRenderOptions Options() => new() { TemplateId = PdfServiceTestHarness.TemplateId };

    [Fact]
    public async Task RenderAsync_ValidHtml_ProducesPdf17Stream()
    {
        using ServiceProvider provider = PdfServiceTestHarness.BuildProvider();
        var svc = provider.GetRequiredService<IMPdfService>();

        using var ms = new MemoryStream();
        PdfRenderResult result = await svc.RenderAsync(Html, ms, Options(), default);

        ms.Length.Should().BeGreaterThan(0);
        result.ByteCount.Should().BeGreaterThan(0);
        result.PageCount.Should().BeGreaterThanOrEqualTo(1);

        ms.Position = 0;
        var head = new byte[8];
        int read = ms.Read(head, 0, 8);
        read.Should().Be(8);
        Encoding.ASCII.GetString(head).Should().Be("%PDF-1.7");
    }

    [Fact]
    public async Task RenderAsync_EmitsActivityWithSnakeCaseTags()
    {
        using ServiceProvider provider = PdfServiceTestHarness.BuildProvider();
        var svc = provider.GetRequiredService<IMPdfService>();

        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == PdfTelemetryNames.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Add,
        };
        ActivitySource.AddActivityListener(listener);

        using var ms = new MemoryStream();
        await svc.RenderAsync(Html, ms, Options(), default);

        stopped.Should().ContainSingle();
        Activity activity = stopped[0];
        activity.OperationName.Should().Be("pdf.render");
        activity.Status.Should().Be(ActivityStatusCode.Ok);

        IReadOnlyDictionary<string, string?> tags = activity.Tags.ToDictionary(t => t.Key, t => t.Value);
        tags.Should().ContainKey(PdfTelemetryNames.TemplateIdTag);
        tags.Should().ContainKey(PdfTelemetryNames.TenantIdTag);
        tags[PdfTelemetryNames.TemplateIdTag].Should().Be(PdfServiceTestHarness.TemplateId);
        tags[PdfTelemetryNames.TenantIdTag].Should().Be(PdfServiceTestHarness.TenantId);
    }

    [Fact]
    public async Task RenderAsync_RecordsPageCountHistogram()
    {
        using ServiceProvider provider = PdfServiceTestHarness.BuildProvider();
        var svc = provider.GetRequiredService<IMPdfService>();

        var pageCounts = new List<int>();
        var operations = new List<(long Value, string? Status)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (inst, l) =>
            {
                if (inst.Meter.Name == PdfTelemetryNames.ActivitySourceName)
                {
                    l.EnableMeasurementEvents(inst);
                }
            },
        };
        listener.SetMeasurementEventCallback<int>((inst, value, _, _) =>
        {
            if (inst.Name == PdfTelemetryNames.PageCountMetric)
            {
                pageCounts.Add(value);
            }
        });
        listener.SetMeasurementEventCallback<long>((inst, value, tags, _) =>
        {
            if (inst.Name == PdfTelemetryNames.OperationMetric)
            {
                string? status = null;
                for (int i = 0; i < tags.Length; i++)
                {
                    if (tags[i].Key == "pdf.status")
                    {
                        status = tags[i].Value as string;
                    }
                }

                operations.Add((value, status));
            }
        });
        listener.Start();

        using var ms = new MemoryStream();
        PdfRenderResult result = await svc.RenderAsync(Html, ms, Options(), default);

        pageCounts.Should().NotBeEmpty();
        pageCounts.Should().Contain(result.PageCount);

        operations.Should().Contain(o => o.Value == 1 && o.Status == "ok");
    }

    [Fact]
    public async Task RenderAsync_ExceedsTimeout_ThrowsOperationCanceled()
    {
        IConfiguration config = PdfServiceTestHarness.ValidConfig(new Dictionary<string, string?>
        {
            ["PdfConfigs:Limits:MaxRenderDurationMs"] = "1",
        });

        var services = new ServiceCollection();
        services.AddTestDoubles(config);
        // Registered BEFORE AddPdf so TryAddSingleton keeps this slow stub. It awaits a delay that
        // observes the linked timeout token, proving the CancelAfter + linked-CTS wiring (PIPE-08).
        services.TryAddSingleton<IHtmlParser>(new SlowHtmlParser());
        services.AddPdf(config);

        using ServiceProvider provider = services.BuildServiceProvider();
        var svc = provider.GetRequiredService<IMPdfService>();

        using var ms = new MemoryStream();
        Func<Task> act = async () => await svc.RenderAsync(Html, ms, Options(), default);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class SlowHtmlParser : IHtmlParser
    {
        public async ValueTask<IParsedDocument> ParseAsync(string html, CancellationToken ct = default)
        {
            await Task.Delay(500, ct).ConfigureAwait(false);
            throw new InvalidOperationException("Should have been cancelled by the render timeout.");
        }
    }
}
