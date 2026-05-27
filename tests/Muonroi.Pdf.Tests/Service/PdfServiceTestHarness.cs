using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Muonroi.Logging.Abstractions;
using Muonroi.Pdf.Extensions;
using Muonroi.Pdf.Internal.Service;
using Muonroi.Pdf.Tests.Writer;
using Muonroi.Tenancy.Abstractions;

namespace Muonroi.Pdf.Tests.Service;

/// <summary>
/// Shared helpers for the DI / integration tests: builds the valid in-memory configuration the
/// Phase 6 plans expect, and registers the test doubles (logging, tenant context, font resolver)
/// that <see cref="MPdfService"/> needs to resolve and render on a headless build host.
/// </summary>
internal static class PdfServiceTestHarness
{
    /// <summary>The HTML template id surfaced in telemetry tags (SC3).</summary>
    public const string TemplateId = "it-template";

    /// <summary>Tenant id the fake <see cref="ITenantContext"/> reports (SC3).</summary>
    public const string TenantId = "tenant-it";

    /// <summary>Builds the canonical valid configuration from the plan's interface block.</summary>
    public static IConfiguration ValidConfig(IDictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["PdfConfigs:Limits:MaxHtmlBytes"] = "8388608",
            ["PdfConfigs:Limits:MaxDomDepth"] = "256",
            ["PdfConfigs:Limits:MaxElementCount"] = "100000",
            ["PdfConfigs:Limits:MaxImagePixels"] = "25000000",
            ["PdfConfigs:Limits:MaxPages"] = "1000",
            ["PdfConfigs:Limits:MaxRenderDurationMs"] = "15000",
            ["PdfConfigs:Limits:MaxFontFiles"] = "32",
        };

        if (overrides != null)
        {
            foreach (KeyValuePair<string, string?> kvp in overrides)
            {
                values[kvp.Key] = kvp.Value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    /// <summary>
    /// Registers the test doubles required for <see cref="MPdfService"/> to resolve:
    /// a no-op logger, a fake tenant context, and an embedded-font resolver. Uses
    /// <c>TryAdd*</c> so callers may pre-register an override before <c>AddPdf</c>.
    /// </summary>
    public static IServiceCollection AddTestDoubles(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // BindConfiguration() resolves IConfiguration from DI at host/provider build time.
        if (configuration != null)
        {
            services.TryAddSingleton(configuration);
        }

        // NSubstitute cannot proxy IMLog<MPdfService> because MPdfService is internal sealed —
        // Castle's dynamic-proxy assembly has no InternalsVisibleTo to the engine. Hand-write a
        // no-op double instead.
        services.TryAddSingleton<IMLog<MPdfService>>(new NoOpLog<MPdfService>());
        services.TryAddSingleton<ITenantContext>(new FakeTenantContext { TenantId = TenantId });
        services.TryAddSingleton<IFontResolver>(new EmbeddedTestFontResolver());
        return services;
    }

    /// <summary>Builds a fully wired provider: test doubles + AddPdf + the supplied config.</summary>
    public static ServiceProvider BuildProvider(IConfiguration? config = null)
    {
        IConfiguration cfg = config ?? ValidConfig();
        var services = new ServiceCollection();
        services.AddTestDoubles(cfg);
        services.AddPdf(cfg);
        return services.BuildServiceProvider();
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public string? TenantId { get; set; }
    }

    /// <summary>No-op <see cref="IMLog{T}"/> double; swallows every log call.</summary>
    private sealed class NoOpLog<T> : IMLog<T>
    {
        private sealed class NoOpScope : IMLogContextScope, IDisposable
        {
            public static readonly NoOpScope Instance = new();
            public void Dispose() { }
        }

        public IMLogContextScope BeginProperty(string key, object? value) => NoOpScope.Instance;
        public void Info(string messageTemplate, params object?[] args) { }
        public void Warn(string messageTemplate, params object?[] args) { }
        public void Error(Exception? ex, string messageTemplate, params object?[] args) { }
        public void Debug(string messageTemplate, params object?[] args) { }
        public void InfoTrace(string messageTemplate, params object?[] args) { }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NoOpScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        { }
    }

    /// <summary>
    /// Resolves every <c>@font-face</c> request to the project's deterministic embedded
    /// <c>TestFont.ttf</c> so renders produce real glyphs without relying on OS-installed fonts.
    /// </summary>
    private sealed class EmbeddedTestFontResolver : IFontResolver
    {
        private static readonly byte[] FontBytes = LoadBytes();

        private static byte[] LoadBytes()
        {
            using Stream stream = typeof(WriterTestFonts).Assembly
                .GetManifestResourceStream("Muonroi.Pdf.Tests.TestResources.TestFont.ttf")
                ?? throw new System.InvalidOperationException("TestFont.ttf embedded resource not found");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return RenameFontInternalName(ms.ToArray());
        }

        // PdfSharpCore keeps a process-global FontFactory cache keyed by the font's internal
        // FontName. The writer tests embed this same TestFont.ttf ("Noto Sans Regular") in full;
        // our integration render embeds a SUBSET of it — same FontName, different bytes — which
        // makes PdfSharpCore throw "same key already added" across test classes. Rewriting the
        // internal name table to a unique, equal-length token gives our copy a distinct FontName
        // so both coexist in the shared cache. Equal-length replacement preserves every name-table
        // offset (and the subsetter copies the name table verbatim).
        private static byte[] RenameFontInternalName(byte[] font)
        {
            ReadOnlySpan<byte> needle = Encoding.BigEndianUnicode.GetBytes("Noto Sans");
            ReadOnlySpan<byte> replacement = Encoding.BigEndianUnicode.GetBytes("Muon ITst"); // 9 chars
            for (int i = 0; i + needle.Length <= font.Length; i++)
            {
                if (font.AsSpan(i, needle.Length).SequenceEqual(needle))
                {
                    replacement.CopyTo(font.AsSpan(i, replacement.Length));
                    i += needle.Length - 1;
                }
            }

            return font;
        }

        public ValueTask<ReadOnlyMemory<byte>?> ResolveAsync(FontRequest request, CancellationToken cancellationToken = default)
            => new(FontBytes);
    }
}
