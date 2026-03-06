namespace Muonroi.Logging;

public static class MLogServiceCollectionExtensions
{
    public static ILoggingBuilder AddMuonroiLogging(this ILoggingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IMLogContext, MLogContext>();
        builder.Services.AddSingleton(typeof(IMLog<>), typeof(MLog<>));
        builder.Services.AddSingleton<ILogScopeFactory, MLogScopeFactory>();
        return builder;
    }
}
