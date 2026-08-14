namespace Muonroi.Diagnostics.Extensions;

/// <summary>
/// Service registration helpers for diagnostics tracing.
/// </summary>
public static class MDiagnosticsServiceCollectionExtensions
{
    /// <summary>
    /// Registers in-memory diagnostics services.
    /// </summary>
    public static IServiceCollection AddMuonroiDiagnostics(this IServiceCollection services)
    {
        services.AddSingleton<IMTraceContext, MTraceContext>();
        services.AddSingleton<ITraceSessionStore, InMemoryTraceSessionStore>();
        return services;
    }

}
