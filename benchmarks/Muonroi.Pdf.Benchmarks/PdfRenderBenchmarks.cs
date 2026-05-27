using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Muonroi.Logging.Abstractions;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Extensions;
using Muonroi.Tenancy.Abstractions;

namespace Muonroi.Pdf.Benchmarks;

/// <summary>
/// BenchmarkDotNet harness for the Muonroi.Pdf engine.
///
/// ALLOC-01 baseline: captures RuntimeFactory Allocated column before any allocation optimisation.
/// SC4: ≥30% allocation reduction in Wave 3 must be measured against this baseline.
/// SC2: ≥3× warm throughput measured by comparing RuntimeFactory vs future SourceGenerated paths.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
public class PdfRenderBenchmarks
{
    private IMPdfService _service = null!;
    private ServiceProvider _serviceProvider = null!;
    private string _html50kb = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Build in-memory configuration with all required PdfConfigs limits.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PdfConfigs:Limits:MaxHtmlBytes"] = "8388608",
                ["PdfConfigs:Limits:MaxDomDepth"] = "256",
                ["PdfConfigs:Limits:MaxElementCount"] = "100000",
                ["PdfConfigs:Limits:MaxImagePixels"] = "25000000",
                ["PdfConfigs:Limits:MaxPages"] = "1000",
                ["PdfConfigs:Limits:MaxRenderDurationMs"] = "15000",
                ["PdfConfigs:Limits:MaxFontFiles"] = "32",
            })
            .Build();

        var services = new ServiceCollection();

        // Register IConfiguration so BindConfiguration() can resolve it.
        services.AddSingleton(configuration);

        // Open-generic no-op logger: satisfies IMLog<MPdfService> (internal) and any other
        // IMLog<T> without requiring InternalsVisibleTo access to MPdfService.
        services.AddSingleton(typeof(IMLog<>), typeof(BenchmarkNoOpLog<>));

        // Font resolver: read a system TTF so PdfSharpCore can embed a font without
        // relying on OS font enumeration, which fails in headless/child-process contexts.
        services.AddSingleton<IFontResolver>(new SystemFontResolver());

        // Fake ITenantContext — engine resolves it per-call via IServiceProvider.GetService<ITenantContext>().
        services.AddSingleton<ITenantContext>(new BenchmarkTenantContext());

        // Register the full PDF engine pipeline.
        services.AddPdf(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _service = _serviceProvider.GetRequiredService<IMPdfService>();

        // Load the reference 50 KB HTML template.
        // File is copied to output directory via <Content CopyToOutputDirectory="Always">.
        string templatePath = Path.Combine(AppContext.BaseDirectory, "reference-50kb.html");
        _html50kb = File.ReadAllText(templatePath);
    }

    /// <summary>
    /// ALLOC-01 baseline: runtime renderer factory path (v0.1 — no allocation optimisation).
    /// This is the "before" snapshot. Wave 3 (Plan 05) optimisations must reduce Allocated ≥30%.
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task RuntimeFactory()
    {
        using var ms = new System.IO.MemoryStream();
        await _service.RenderAsync(_html50kb, ms, new PdfRenderOptions());
    }

    /// <summary>
    /// SourceGenerated benchmark slot — wired to the runtime path in Wave 1.
    /// TODO(Wave3): swap _service.RenderAsync for _sgRenderer.RenderAsync after Plan 01 SG is wired.
    /// When the SG renderer is available, this benchmark measures SC2 (≥3× warm throughput vs RuntimeFactory).
    /// </summary>
    [Benchmark]
    public async Task SourceGenerated()
    {
        // TODO(Wave3): swap _service.RenderAsync for _sgRenderer.RenderAsync after Plan 01 SG is wired
        using var ms = new System.IO.MemoryStream();
        await _service.RenderAsync(_html50kb, ms, new PdfRenderOptions());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        (_serviceProvider as IDisposable)?.Dispose();
    }

    // -------------------------------------------------------------------------
    // Private benchmark infrastructure doubles
    // -------------------------------------------------------------------------

    private sealed class BenchmarkTenantContext : ITenantContext
    {
        public string? TenantId { get; set; } = "benchmark-tenant";
    }

    /// <summary>
    /// Resolves every @font-face request to Arial from the Windows system fonts directory.
    /// Provides a real TTF so PdfSharpCore can embed a font without OS font enumeration,
    /// which fails in BDN's isolated child-process context.
    /// </summary>
    private sealed class SystemFontResolver : IFontResolver
    {
        // Arial is present on all Windows machines; fall back to any .ttf in Windows\Fonts.
        private static readonly byte[] FontBytes = LoadFont();

        private static byte[] LoadFont()
        {
            string[] candidates =
            [
                @"C:\Windows\Fonts\arial.ttf",
                @"C:\Windows\Fonts\ARIALN.TTF",
                @"C:\Windows\Fonts\calibri.ttf",
            ];

            foreach (string path in candidates)
            {
                if (File.Exists(path))
                {
                    return File.ReadAllBytes(path);
                }
            }

            // Last resort: return the first .ttf found in the system fonts directory.
            string fontsDir = @"C:\Windows\Fonts";
            if (Directory.Exists(fontsDir))
            {
                string? first = Directory.EnumerateFiles(fontsDir, "*.ttf").FirstOrDefault();
                if (first != null)
                {
                    return File.ReadAllBytes(first);
                }
            }

            throw new InvalidOperationException(
                "No system TTF font found. Benchmark requires at least one .ttf in C:\\Windows\\Fonts.");
        }

        public ValueTask<ReadOnlyMemory<byte>?> ResolveAsync(
            FontRequest request, CancellationToken cancellationToken = default)
            => new(new ReadOnlyMemory<byte>(FontBytes));
    }

    /// <summary>
    /// Open-generic no-op IMLog&lt;T&gt; — satisfies the IMLog&lt;MPdfService&gt; constructor
    /// parameter without requiring access to the internal MPdfService type.
    /// </summary>
    private sealed class BenchmarkNoOpLog<T> : IMLog<T>
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
}
