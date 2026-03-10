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
    /// <returns>The same <see cref="ILoggingBuilder"/> instance so that multiple calls can be chained.</returns>
    public static ILoggingBuilder AddMuonroiLogging(this ILoggingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IMLogContext, MLogContext>();
        builder.Services.AddSingleton(typeof(IMLog<>), typeof(MLog<>));
        builder.Services.AddSingleton<IMLogFactory, MLogFactory>();
        builder.Services.AddSingleton<ILogScopeFactory, MLogScopeFactory>();
        return builder;
    }
}
