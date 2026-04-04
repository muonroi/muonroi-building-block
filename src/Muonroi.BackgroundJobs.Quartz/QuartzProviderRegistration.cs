using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.BackgroundJobs.Abstractions;

namespace Muonroi.BackgroundJobs.Quartz;

/// <summary>
/// Module initializer that self-registers the Quartz provider with
/// <see cref="BackgroundJobHandler.RegisterProvider"/> at assembly load time.
/// This is AOT-safe — no reflection, compile-time delegate only.
/// </summary>
internal static class QuartzProviderRegistration
{
    /// <summary>
    /// Registers the Quartz provider when the assembly is loaded.
    /// </summary>
    [ModuleInitializer]
    internal static void Register()
    {
        BackgroundJobHandler.RegisterProvider(
            JobType.Quartz,
            static (IServiceCollection services, IConfiguration configuration) =>
                Quartz.BackgroundJobHandler.AddBackgroundJobs(services, configuration));
    }
}
