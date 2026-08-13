using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Logging;

/// <summary>
/// Provides extension methods for registering Muonroi logging services in an <see cref="ILoggingBuilder"/>.
/// </summary>
public static class MLogServiceCollectionExtensions
{
    /// <summary>
    /// Registers Muonroi logging services, including <see cref="IMLogContext"/> and generic <see cref="IMLog{T}"/>.
    /// </summary>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to configure.</param>
    /// <param name="useAsyncQueue">Optional. Set to false for synchronous testing.</param>
    /// <returns>The same <see cref="ILoggingBuilder"/> instance so that multiple calls can be chained.</returns>
    public static ILoggingBuilder AddMuonroiLogging(this ILoggingBuilder builder, bool useAsyncQueue = true)
    {
        MGuard.NotNull(builder);

        builder.Services.GetOrCreateRegistry().Register(MCapability.Logging);

        builder.Services.AddSingleton<IMLogContext, MLogContext>();
        builder.Services.AddSingleton(typeof(IMLog<>), typeof(MLog<>));
        builder.Services.AddSingleton<IMLogFactory, MLogFactory>();
        builder.Services.AddSingleton<ILogScopeFactory, MLogScopeFactory>();
        
        // Register default gap implementations for Argument Resolver and Exception Classifier
        builder.Services.AddSingleton<Muonroi.Logging.Abstractions.IMLogArgumentResolver, MLogArgumentResolver>();
        builder.Services.AddSingleton<Muonroi.Logging.Abstractions.Exceptions.IExceptionClassifier, Muonroi.Logging.Exceptions.DefaultExceptionClassifier>();
        builder.Services.AddSingleton<Muonroi.Logging.Abstractions.IInterceptedLogWriter, InterceptedLogWriter>();
        
        if (useAsyncQueue)
        {
            // Register Queueing
            builder.Services.AddSingleton<Muonroi.Logging.Queueing.IMuonroiLogQueue, Muonroi.Logging.Queueing.MuonroiLogQueue>();
            builder.Services.AddSingleton<Muonroi.Logging.Queueing.DiskBufferStore>();
            // Note: Hosted service will be shut down last if it is registered first, but ILoggingBuilder is usually called after standard host setup.
            // We register it here and recommend users call AddMuonroiLogging() early.
            builder.Services.AddHostedService<Muonroi.Logging.Queueing.LogBackgroundProcessor>();
            
            // Register ObjectPool for LogEvent
            builder.Services.AddSingleton<Microsoft.Extensions.ObjectPool.ObjectPoolProvider, Microsoft.Extensions.ObjectPool.DefaultObjectPoolProvider>();
            builder.Services.AddSingleton<Microsoft.Extensions.ObjectPool.ObjectPool<Muonroi.Logging.Abstractions.Models.LogEvent>>(sp =>
            {
                var provider = sp.GetRequiredService<Microsoft.Extensions.ObjectPool.ObjectPoolProvider>();
                return provider.Create(new Muonroi.Logging.Queueing.LogEventPooledObjectPolicy());
            });
        }

        return builder;
    }
}
