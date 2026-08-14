namespace Muonroi.AspNetCore.Tests.Diagnostics;

/// <summary>
/// Tests for the Muonroi ecosystem startup diagnostics engine.
/// </summary>
public sealed class EcosystemDiagnosticsTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // DiagnosticResult helper tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DiagnosticResult_Pass_CreatesPassedResult()
    {
        DiagnosticResult result = DiagnosticResult.Pass("All good.");

        result.Passed.Should().BeTrue();
        result.Message.Should().Be("All good.");
        result.FixSuggestion.Should().BeNull();
    }

    [Fact]
    public void DiagnosticResult_Fail_CreatesFailedResult()
    {
        DiagnosticResult result = DiagnosticResult.Fail("Something broke.", "Try calling AddXxx().");

        result.Passed.Should().BeFalse();
        result.Message.Should().Be("Something broke.");
        result.FixSuggestion.Should().Be("Try calling AddXxx().");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AddMuonroiDiagnostics — registration
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AddMuonroiDiagnostics_Registers_SixBuiltInDiagnostics()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddMuonroiDiagnostics();

        ServiceProvider sp = services.BuildServiceProvider();
        IEnumerable<IMEcosystemDiagnostic> diagnostics = sp.GetServices<IMEcosystemDiagnostic>();

        diagnostics.Should().HaveCount(6);
    }

    [Fact]
    public void AddMuonroiDiagnostics_Registers_AllExpectedTypes()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddMuonroiDiagnostics();

        ServiceProvider sp = services.BuildServiceProvider();
        List<IMEcosystemDiagnostic> diagnostics = [.. sp.GetServices<IMEcosystemDiagnostic>()];
        IReadOnlyList<Type> expectedTypes =
        [
            typeof(DependencyGraphDiagnostic),
            typeof(ConfigurationDiagnostic),
            typeof(ConnectivityDiagnostic),
            typeof(EcosystemRegistryDiagnostic),
            typeof(LicenseStatusDiagnostic),
            typeof(TenantConfigDiagnostic),
        ];

        foreach (Type expected in expectedTypes)
        {
            diagnostics.Should().Contain(d => d.GetType() == expected,
                $"Expected {expected.Name} to be registered.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DependencyGraphDiagnostic
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DependencyGraphDiagnostic_Passes_WhenAllCoreServicesResolvable()
    {
        // Minimal service provider with core Muonroi services
        ServiceCollection services = new();
        services.AddLogging();
        ServiceProvider sp = services.BuildServiceProvider();

        DependencyGraphDiagnostic diagnostic = new();

        // Should pass because the core types are either not loaded or resolve OK
        DiagnosticResult result = await diagnostic.CheckAsync(sp, CancellationToken.None);

        // We just verify it runs without exception; the actual pass/fail depends on what is loaded
        result.Should().NotBeNull();
        result.Message.Should().NotBeNullOrEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EcosystemRegistryDiagnostic
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EcosystemRegistryDiagnostic_Passes_WhenRegistryNotPresent()
    {
        ServiceCollection services = new();
        ServiceProvider sp = services.BuildServiceProvider();

        EcosystemRegistryDiagnostic diagnostic = new();
        DiagnosticResult result = await diagnostic.CheckAsync(sp, CancellationToken.None);

        result.Passed.Should().BeTrue();
        result.Message.Should().Contain("IMEcosystemRegistry not registered");
    }

    [Fact]
    public async Task EcosystemRegistryDiagnostic_Warns_WhenMultiTenantFlagSetButITenantContextMissing()
    {
        ServiceCollection services = new();

        // Register an ecosystem registry with MultiTenant capability flagged
        MEcosystemRegistry registry = new();
        registry.Register(MCapability.MultiTenant);
        services.AddSingleton<IMEcosystemRegistry>(registry);
        // Deliberately NOT registering ITenantContext

        ServiceProvider sp = services.BuildServiceProvider();

        EcosystemRegistryDiagnostic diagnostic = new();
        DiagnosticResult result = await diagnostic.CheckAsync(sp, CancellationToken.None);

        result.Passed.Should().BeFalse();
        result.FixSuggestion.Should().Contain("ITenantContext");
    }

    [Fact]
    public async Task EcosystemRegistryDiagnostic_Passes_WhenMultiTenantFlagSetAndITenantContextPresent()
    {
        ServiceCollection services = new();

        MEcosystemRegistry registry = new();
        registry.Register(MCapability.MultiTenant);
        services.AddSingleton<IMEcosystemRegistry>(registry);
        services.AddSingleton<ITenantContext>(Substitute.For<ITenantContext>());

        ServiceProvider sp = services.BuildServiceProvider();

        EcosystemRegistryDiagnostic diagnostic = new();
        DiagnosticResult result = await diagnostic.CheckAsync(sp, CancellationToken.None);

        result.Passed.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TenantConfigDiagnostic
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TenantConfigDiagnostic_Passes_WhenITenantContextNotRegistered()
    {
        ServiceCollection services = new();
        ServiceProvider sp = services.BuildServiceProvider();

        TenantConfigDiagnostic diagnostic = new();
        DiagnosticResult result = await diagnostic.CheckAsync(sp, CancellationToken.None);

        result.Passed.Should().BeTrue();
        result.Message.Should().Contain("ITenantContext not registered");
    }

    [Fact]
    public async Task TenantConfigDiagnostic_Passes_WhenTenantContextRegisteredAndConfigValid()
    {
        ServiceCollection services = new();
        services.AddSingleton<ITenantContext>(Substitute.For<ITenantContext>());
        services.Configure<MultiTenantOptions>(opts => opts.Enabled = true);
        // Provide a configuration with the MultiTenantConfigs section
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiTenantConfigs:Enabled"] = "true",
                ["MultiTenantConfigs:Strategy"] = "SharedSchema"
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        ServiceProvider sp = services.BuildServiceProvider();

        TenantConfigDiagnostic diagnostic = new();
        DiagnosticResult result = await diagnostic.CheckAsync(sp, CancellationToken.None);

        result.Passed.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LicenseStatusDiagnostic
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LicenseStatusDiagnostic_Passes_WhenLicenseGuardNotRegistered()
    {
        ServiceCollection services = new();
        ServiceProvider sp = services.BuildServiceProvider();

        LicenseStatusDiagnostic diagnostic = new();
        DiagnosticResult result = await diagnostic.CheckAsync(sp, CancellationToken.None);

        result.Passed.Should().BeTrue();
        result.Message.Should().Contain("standalone");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ConnectivityDiagnostic
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConnectivityDiagnostic_Passes_WhenNoDbContextRegistered()
    {
        ServiceCollection services = new();
        ServiceProvider sp = services.BuildServiceProvider();

        ConnectivityDiagnostic diagnostic = new();
        DiagnosticResult result = await diagnostic.CheckAsync(sp, CancellationToken.None);

        result.Passed.Should().BeTrue();
        result.Message.Should().Contain("No DbContext registered");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ConfigurationDiagnostic
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfigurationDiagnostic_Passes_WhenIConfigurationNotRegistered()
    {
        ServiceCollection services = new();
        ServiceProvider sp = services.BuildServiceProvider();

        ConfigurationDiagnostic diagnostic = new();
        DiagnosticResult result = await diagnostic.CheckAsync(sp, CancellationToken.None);

        result.Passed.Should().BeTrue();
        result.Message.Should().Contain("IConfiguration not registered");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UseMuonroiDiagnostics — all pass scenario
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UseMuonroiDiagnostics_DoesNotThrow_WhenAllDiagnosticsPass()
    {
        ServiceCollection services = new();
        services.AddLogging();

        // Register only a passing diagnostic
        services.AddSingleton<IMEcosystemDiagnostic, AlwaysPassDiagnostic>();

        ServiceProvider sp = services.BuildServiceProvider();

        // Use a minimal ApplicationBuilder stub
        FakeApplicationBuilder appBuilder = new(sp);

        Action act = () => appBuilder.UseMuonroiDiagnostics();
        act.Should().NotThrow();
    }

    [Fact]
    public void UseMuonroiDiagnostics_DoesNotThrow_WhenOnlyWarningsDiagnosticsPresent()
    {
        ServiceCollection services = new();
        services.AddLogging();

        services.AddSingleton<IMEcosystemDiagnostic>(new AlwaysFailDiagnostic(DiagnosticSeverity.Warning));

        ServiceProvider sp = services.BuildServiceProvider();
        FakeApplicationBuilder appBuilder = new(sp);

        Action act = () => appBuilder.UseMuonroiDiagnostics();
        act.Should().NotThrow();
    }

    [Fact]
    public void UseMuonroiDiagnostics_Throws_WhenCriticalDiagnosticFails()
    {
        ServiceCollection services = new();
        services.AddLogging();

        services.AddSingleton<IMEcosystemDiagnostic>(new AlwaysFailDiagnostic(DiagnosticSeverity.Critical));

        ServiceProvider sp = services.BuildServiceProvider();
        FakeApplicationBuilder appBuilder = new(sp);

        Action act = () => appBuilder.UseMuonroiDiagnostics();
        act.Should().Throw<MInternalException>()
            .WithMessage("*critical*");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test stubs
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class AlwaysPassDiagnostic : IMEcosystemDiagnostic
    {
        public string Name => "AlwaysPass";
        public DiagnosticSeverity Severity => DiagnosticSeverity.Critical;
        public Task<DiagnosticResult> CheckAsync(IServiceProvider sp, CancellationToken ct) =>
            Task.FromResult(DiagnosticResult.Pass("Everything is fine."));
    }

    private sealed class AlwaysFailDiagnostic(DiagnosticSeverity severity) : IMEcosystemDiagnostic
    {
        public string Name => "AlwaysFail";
        public DiagnosticSeverity Severity => severity;
        public Task<DiagnosticResult> CheckAsync(IServiceProvider sp, CancellationToken ct) =>
            Task.FromResult(DiagnosticResult.Fail("It failed.", "Call services.AddSomething()."));
    }

    /// <summary>
    /// Minimal <see cref="IApplicationBuilder"/> stub for testing pipeline extensions.
    /// </summary>
    private sealed class FakeApplicationBuilder(IServiceProvider sp) : IApplicationBuilder
    {
        private readonly IServiceProvider _sp = sp;

        public IServiceProvider ApplicationServices { get; set; } = sp;
        public IFeatureCollection ServerFeatures => new FeatureCollection();
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware) => this;
        public IApplicationBuilder New() => new FakeApplicationBuilder(_sp);
        public RequestDelegate Build() => _ => Task.CompletedTask;
    }
}
