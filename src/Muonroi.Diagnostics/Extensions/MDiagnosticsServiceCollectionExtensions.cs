using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Diagnostics.Abstractions;
using Muonroi.Diagnostics.Context;
using Muonroi.Diagnostics.Store;

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

    /// <summary>
    /// Registers Redis-backed diagnostics services.
    /// </summary>
    public static IServiceCollection AddMuonroiDiagnosticsRedis(this IServiceCollection services)
    {
        services.AddSingleton<IMTraceContext, MTraceContext>();
        services.AddSingleton<ITraceSessionStore, RedisTraceSessionStore>();
        return services;
    }
}
