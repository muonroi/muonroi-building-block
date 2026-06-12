using System.Runtime.CompilerServices;

namespace Muonroi.BackgroundJobs.Hangfire;

/// <summary>
/// Module initializer that self-registers the Hangfire provider with
/// <see cref="BackgroundJobHandler.RegisterProvider"/> at assembly load time.
/// This is AOT-safe — no reflection, compile-time delegate only.
/// </summary>
internal static class HangfireProviderRegistration
{
    /// <summary>
    /// Registers the Hangfire provider when the assembly is loaded.
    /// </summary>
    [ModuleInitializer]
    internal static void Register()
    {
        BackgroundJobHandler.RegisterProvider(
            JobType.Hangfire,
            static (IServiceCollection services, IConfiguration configuration) =>
                Hangfire.BackgroundJobHandler.AddBackgroundJobs(services, configuration));
    }
}
