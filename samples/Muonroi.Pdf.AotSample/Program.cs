// AOT-03 smoke test: renders a minimal HTML document to PDF using the Muonroi.Pdf engine.
// This sample intentionally omits Muonroi.Observability (OtelSetup has AOT-incompatible
// AppDomain.CurrentDomain.GetAssemblies() + Activator.CreateInstance) and Muonroi.Tenancy
// (ITenantContextPolicy has AppDomain reflection).

using Microsoft.Extensions.Logging;

try
{
    IConfigurationRoot config = new ConfigurationBuilder().Build();
    var services = new ServiceCollection();

    // Register IConfiguration so BindConfiguration() inside AddPdf() can resolve it.
    services.AddSingleton<IConfiguration>(config);

    // Open-generic no-op IMLog<T> — MPdfService requires IMLog<MPdfService> which is
    // internal to Muonroi.Pdf. The open-generic registration satisfies all IMLog<T> requests.
    services.AddSingleton(typeof(IMLog<>), typeof(AotNoOpLog<>));

    // Font resolver: serves an embedded TTF (SampleFont.ttf) so OwnedPdfWriter can embed a font
    // with zero dependency on OS-installed fonts (none exist on the Alpine runtime image).
    services.AddSingleton<IFontResolver>(new AotFontResolver());

    services.AddPdf(config);
    ServiceProvider provider = services.BuildServiceProvider();
    IMPdfService pdfService = provider.GetRequiredService<IMPdfService>();

    // The @font-face declaration drives the FontPipeline to invoke the registered IFontResolver;
    // without it the writer has no embedded font and text glyphs would not render. The face is
    // declared under the "serif" family because the box tree assigns synthesized inline text the
    // default family "serif" (block-level font-family is not inherited down to inline text nodes).
    // The src url is nominal: AotFontResolver returns the embedded SampleFont bytes for any request.
    const string html =
        "<!DOCTYPE html><html><head><style>" +
        "@font-face{font-family:serif;src:url('sample.ttf');}" +
        "</style></head><body><h1>AOT Sample</h1><p>Hello from NativeAOT.</p></body></html>";
    var outputPath = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "aot-sample-output.pdf");

    await using FileStream output = File.OpenWrite(outputPath);
    PdfRenderResult result = await pdfService.RenderAsync(html, output, new PdfRenderOptions());
    Console.WriteLine($"OK: {result.PageCount}p {result.ByteCount}b -> {outputPath}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"AOT render FAILED: {ex}");
    return 1;
}

/// <summary>
/// Self-contained font resolver for the AOT sample. Loads the font from an embedded resource
/// (SampleFont.ttf) so the binary renders on any platform — including Alpine with no OS fonts —
/// and stays valid under single-file NativeAOT publish.
/// </summary>
internal sealed class AotFontResolver : IFontResolver
{
    private const string ResourceName = "Muonroi.Pdf.AotSample.SampleFont.ttf";
    private static readonly byte[] FontBytes = LoadFont();

    private static byte[] LoadFont()
    {
        using Stream stream = typeof(AotFontResolver).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded font resource not found: {ResourceName}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public ValueTask<ReadOnlyMemory<byte>?> ResolveAsync(
        FontRequest request, CancellationToken cancellationToken = default)
        => new(new ReadOnlyMemory<byte>(FontBytes));
}

/// <summary>
/// AOT-compatible no-op IMLog&lt;T&gt; implementation.
/// Open-generic registration satisfies IMLog&lt;MPdfService&gt; (internal type).
/// </summary>
internal sealed class AotNoOpLog<T> : IMLog<T>
{
    private sealed class NullScope : IMLogContextScope
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }

    public IMLogContextScope BeginProperty(string key, object? value) => NullScope.Instance;
    public void Info(string messageTemplate, params object?[] args) { }
    public void Warn(string messageTemplate, params object?[] args) { }
    public void Error(Exception? ex, string messageTemplate, params object?[] args) { }
    public void Debug(string messageTemplate, params object?[] args) { }
    public void InfoTrace(string messageTemplate, params object?[] args) { }
    public void InfoContext(string messageTemplate, params object?[] args) { }
    public void InfoContext(string messageTemplate, object? arg0 = null, object? arg1 = null, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0) { }
    public void ErrorContext(Exception? ex, string messageTemplate, params object?[] args) { }
    public void ErrorContext(Exception? ex, string messageTemplate, object? arg0 = null, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0) { }
    public void Audit(string messageTemplate, params object?[] args) { }
    public void Audit(string messageTemplate, string? auditType = null, string? action = null, bool isSuccess = true, string? targetId = null, string? targetType = null, object? metadata = null, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0) { }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    { }
}
