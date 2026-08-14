namespace Muonroi.Pdf.Tests.Policy;

/// <summary>
/// Phase 11.4: LegacyPrintPolicy soft-degrade option for flex/grid.
/// Tests the opt-in <c>PdfPolicySettings.SoftDegradeUnknownDisplay</c> behavior while verifying
/// that the default strict behavior is unchanged.
/// </summary>
public sealed class LegacyPrintPolicySoftDegradeTests
{
    private static async Task<IPdfDocumentContext> ParseAsync(string html)
    {
        var parser = new AngleSharpHtmlParser();
        IParsedDocument parsed = await parser.ParseAsync(html).ConfigureAwait(false);
        var cascade = new AngleSharpCascadeEngine();
        IStyledDocument doc = await cascade.CascadeAsync(parsed, null).ConfigureAwait(false);
        return (IPdfDocumentContext)doc;
    }

    private static LegacyPrintPolicy PolicyWithSoftDegrade(bool softDegrade)
    {
        var cfg = new PdfConfigs
        {
            Policy = new PdfPolicySettings { SoftDegradeUnknownDisplay = softDegrade }
        };
        return new LegacyPrintPolicy(Options.Create(cfg));
    }

    // --------------------------------------------------------------------------
    // Test 1: default (strict) config — display:flex → hard Error, Accepted=false
    // --------------------------------------------------------------------------

    [Fact]
    public async Task LegacyPrintPolicy_strict_default_rejects_flex()
    {
        const string html = "<html><head><style>div{display:flex;}</style></head><body><div>x</div></body></html>";
        var context = await ParseAsync(html);

        var policy = new LegacyPrintPolicy(); // parameterless = strict

        PolicyValidationResult result = await policy.ValidateAsync(context);

        result.Accepted.Should().BeFalse(because: "default strict mode must reject display:flex");
        result.Violations.Should().Contain(v =>
            v.RuleId == "forbidden.display.flex" && v.Severity == PolicySeverity.Error,
            because: "default strict mode must emit an Error-severity violation for display:flex");
    }

    // --------------------------------------------------------------------------
    // Test 2: soft-degrade on — display:flex → Warning, Accepted=true, RuleId starts with soft-degrade.
    // --------------------------------------------------------------------------

    [Fact]
    public async Task LegacyPrintPolicy_soft_degrade_accepts_flex_as_warning()
    {
        const string html = "<html><head><style>div{display:flex;}</style></head><body><div>x</div></body></html>";
        var context = await ParseAsync(html);

        LegacyPrintPolicy policy = PolicyWithSoftDegrade(softDegrade: true);

        PolicyValidationResult result = await policy.ValidateAsync(context);

        result.Accepted.Should().BeTrue(because: "soft-degrade mode must not reject display:flex");
        result.Violations.Should().ContainSingle(v =>
            v.RuleId.StartsWith("soft-degrade.", StringComparison.Ordinal) &&
            v.Severity == PolicySeverity.Warning,
            because: "soft-degrade must emit exactly one Warning violation for the flex element");
    }

    // --------------------------------------------------------------------------
    // Test 3: full render path — soft-degrade on, HTML with display:flex divs → PDF bytes > 0
    // --------------------------------------------------------------------------

    [Fact]
    public async Task LegacyPrintPolicy_soft_degrade_renders_flex_as_block()
    {
        // Two children with display:flex should render stacked vertically (block fallback).
        const string html =
            "<html><head><style>" +
            "@font-face{font-family:serif;src:url(test.ttf);}" +
            ".row{display:flex;gap:8px;}" +
            "</style></head>" +
            "<body>" +
            "<div class=\"row\"><div>Child A</div><div>Child B</div></div>" +
            "<div class=\"row\"><div>Child C</div><div>Child D</div></div>" +
            "</body></html>";

        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["PdfConfigs:Limits:MaxHtmlBytes"] = "8388608",
                ["PdfConfigs:Limits:MaxDomDepth"] = "256",
                ["PdfConfigs:Limits:MaxElementCount"] = "100000",
                ["PdfConfigs:Limits:MaxImagePixels"] = "25000000",
                ["PdfConfigs:Limits:MaxPages"] = "1000",
                ["PdfConfigs:Limits:MaxRenderDurationMs"] = "15000",
                ["PdfConfigs:Limits:MaxFontFiles"] = "32",
                ["PdfConfigs:Policy:SoftDegradeUnknownDisplay"] = "true"
            }).Build();

        var services = new ServiceCollection();
        services.AddTestDoubles(cfg);
        services.AddPdf(cfg);
        using ServiceProvider provider = services.BuildServiceProvider();

        var svc = provider.GetRequiredService<IMPdfService>();
        using var ms = new MemoryStream();

        Func<Task> render = () => svc.RenderAsync(html, ms, new PdfRenderOptions { TemplateId = "soft-degrade-test" }, default);
        await render.Should().NotThrowAsync(because: "soft-degrade mode must not throw on display:flex HTML");

        ms.Length.Should().BeGreaterThan(0, because: "render must produce non-empty PDF bytes");
    }

    // --------------------------------------------------------------------------
    // Test 4: telemetry counter increments when soft-degrade triggers
    // --------------------------------------------------------------------------

    [Fact]
    public async Task LegacyPrintPolicy_soft_degrade_telemetry_counter_increments()
    {
        const string html =
            "<html><head><style>div{display:flex;}</style></head><body><div>x</div></body></html>";
        var context = await ParseAsync(html);

        LegacyPrintPolicy policy = PolicyWithSoftDegrade(softDegrade: true);

        long flexBefore = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == PdfTelemetryNames.ActivitySourceName &&
                instrument.Name == PdfTelemetryNames.PolicySoftDegradeMetric)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == PdfTelemetryNames.PolicySoftDegradeMetric)
            {
                // Verify the "kind" tag is "flex" or "grid"
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    if (tag.Key == "kind" && tag.Value is string kind && kind == "flex")
                        Interlocked.Add(ref flexBefore, measurement);
                }
            }
        });
        listener.Start();

        await policy.ValidateAsync(context);

        listener.RecordObservableInstruments();

        flexBefore.Should().BeGreaterThan(0,
            because: "soft-degrade counter muonroi_pdf_policy_soft_degrade_total must increment with kind=flex");
    }
}
