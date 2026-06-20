using System.Diagnostics.CodeAnalysis;
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
    [SuppressMessage("Usage", "CA2255", Justification = "Intentional one-time module initializer to register provider on assembly load.")]
    internal static void Register()
    {
        BackgroundJobHandler.RegisterProvider(
            JobType.Hangfire,
            static (IServiceCollection services, IConfiguration configuration) =>
                Hangfire.BackgroundJobHandler.AddBackgroundJobs(services, configuration));
    }
}
