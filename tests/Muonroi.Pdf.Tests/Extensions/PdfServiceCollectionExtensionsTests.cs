namespace Muonroi.Pdf.Tests.Extensions;

/// <summary>
/// Phase 11.1 — verifies that <see cref="PdfServiceCollectionExtensions.AddPdf"/> auto-wires
/// Muonroi.Logging and <see cref="ISystemExecutionContextAccessor"/>, and that a consumer-supplied
/// accessor is preserved (TryAdd contract).
/// </summary>
public sealed class PdfServiceCollectionExtensionsTests
{
    /// <summary>
    /// AddPdf must self-register enough infrastructure (Muonroi.Logging +
    /// ISystemExecutionContextAccessor) that IMPdfService resolves without supplying
    /// logging or context doubles. A no-op IFontResolver is supplied to satisfy
    /// DefaultFontResolver's TryAdd (which needs IHostEnvironment) — this is the same
    /// minimal-doubles pattern used by PdfServiceTestHarness.
    /// </summary>
    [Fact]
    public void AddPdf_self_registers_logging_and_exec_context()
    {
        IConfiguration config = BuildMinimalConfig();
        var services = new ServiceCollection();
        // BindConfiguration() resolves IConfiguration from DI — register the instance directly.
        services.AddSingleton(config);
        // Supply a no-op IFontResolver so DefaultFontResolver's TryAdd (which needs
        // IHostEnvironment) does not win the slot and trigger an unresolvable dependency.
        services.AddSingleton<IFontResolver>(new NoOpFontResolver());

        // No other test doubles — AddPdf must supply Logging + ISystemExecutionContextAccessor.
        services.AddPdf(config);

        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        Action resolve = () => provider.GetRequiredService<IMPdfService>();
        resolve.Should().NotThrow("AddPdf must self-register Muonroi.Logging so IMPdfService resolves");

        ISystemExecutionContextAccessor? accessor =
            provider.GetService<ISystemExecutionContextAccessor>();
        accessor.Should().NotBeNull("AddPdf must TryAdd ISystemExecutionContextAccessor");
    }

    /// <summary>
    /// A consumer that pre-registers a custom <see cref="ISystemExecutionContextAccessor"/>
    /// before calling AddPdf must get their instance back (TryAdd contract).
    /// </summary>
    [Fact]
    public void AddPdf_preserves_consumer_exec_context_override()
    {
        IConfiguration config = BuildMinimalConfig();
        var services = new ServiceCollection();
        services.AddSingleton(config);
        services.AddSingleton<IFontResolver>(new NoOpFontResolver());

        var stub = new StubExecutionContextAccessor();
        services.AddSingleton<ISystemExecutionContextAccessor>(stub);

        services.AddPdf(config);

        using ServiceProvider provider = services.BuildServiceProvider();

        ISystemExecutionContextAccessor resolved =
            provider.GetRequiredService<ISystemExecutionContextAccessor>();

        resolved.Should().BeSameAs(stub,
            "TryAdd must not overwrite a pre-registered ISystemExecutionContextAccessor");
    }

    /// <summary>
    /// Phase 11.3 — a consumer that pre-registers a custom <see cref="IFontResolver"/> before
    /// calling AddPdf must get their instance back (TryAdd contract).
    /// </summary>
    [Fact]
    public void AddPdf_default_resolver_overridable()
    {
        IConfiguration config = BuildMinimalConfig();
        var services = new ServiceCollection();
        services.AddSingleton(config);

        // Pre-register a stub resolver BEFORE AddPdf.
        var stubResolver = new NoOpFontResolver();
        services.AddSingleton<IFontResolver>(stubResolver);

        services.AddPdf(config);

        using ServiceProvider provider = services.BuildServiceProvider();

        IFontResolver resolved = provider.GetRequiredService<IFontResolver>();

        resolved.Should().BeSameAs(stubResolver,
            "TryAdd must not overwrite a pre-registered IFontResolver (consumer override wins)");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static IConfiguration BuildMinimalConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PdfConfigs:Limits:MaxHtmlBytes"]        = "8388608",
                ["PdfConfigs:Limits:MaxDomDepth"]         = "256",
                ["PdfConfigs:Limits:MaxElementCount"]     = "100000",
                ["PdfConfigs:Limits:MaxImagePixels"]      = "25000000",
                ["PdfConfigs:Limits:MaxPages"]            = "1000",
                ["PdfConfigs:Limits:MaxRenderDurationMs"] = "15000",
                ["PdfConfigs:Limits:MaxFontFiles"]        = "32",
            })
            .Build();

    private sealed class StubExecutionContextAccessor : ISystemExecutionContextAccessor
    {
        public ISystemExecutionContext Get() => SystemExecutionContext.Empty;
        public void Set(ISystemExecutionContext context) { }
        public void Clear() { }
    }

    private sealed class NoOpFontResolver : IFontResolver
    {
        public ValueTask<ReadOnlyMemory<byte>?> ResolveAsync(
            FontRequest request,
            CancellationToken cancellationToken = default)
            => new(default(ReadOnlyMemory<byte>?));
    }
}
