using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Diagnostics.Abstractions;
using Muonroi.Diagnostics.Context;
using Muonroi.Diagnostics.Store;

namespace Muonroi.Diagnostics.Extensions;

public static class MDiagnosticsServiceCollectionExtensions
{
    public static IServiceCollection AddMuonroiDiagnostics(this IServiceCollection services)
    {
        services.AddSingleton<IMTraceContext, MTraceContext>();
        services.AddSingleton<ITraceSessionStore, InMemoryTraceSessionStore>();
        return services;
    }

    public static IServiceCollection AddMuonroiDiagnosticsRedis(this IServiceCollection services)
    {
        services.AddSingleton<IMTraceContext, MTraceContext>();
        services.AddSingleton<ITraceSessionStore, RedisTraceSessionStore>();
        return services;
    }
}
