namespace Muonroi.Diagnostics.Tests;

public class MDiagnosticsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMuonroiDiagnostics_Should_Register_TraceContext()
    {
        ServiceCollection services = new();
        services.AddSingleton(Mock.Of<IMJsonSerializeService>());
        services.AddMuonroiDiagnostics();

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<IMTraceContext>().Should().NotBeNull();
    }

    [Fact]
    public void AddMuonroiDiagnostics_Should_Register_InMemoryStore()
    {
        ServiceCollection services = new();
        services.AddSingleton(Mock.Of<IMJsonSerializeService>());
        services.AddMuonroiDiagnostics();

        using ServiceProvider provider = services.BuildServiceProvider();
        ITraceSessionStore store = provider.GetRequiredService<ITraceSessionStore>();
        store.Should().BeOfType<InMemoryTraceSessionStore>();
    }

}
