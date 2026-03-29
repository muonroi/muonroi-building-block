namespace Muonroi.Tenancy.SiteProfile.Web.Pipeline;

/// <summary>
/// Options bag for accumulating hook registrations during DI configuration.
/// Consumed by <see cref="SitePipelineHookRegistry"/> at startup.
/// </summary>
public sealed class SitePipelineHookOptions
{
    internal List<HookRegistration> Hooks { get; } = [];
}

/// <summary>
/// Represents a single hook registration captured during DI configuration.
/// </summary>
internal sealed record HookRegistration(
    string SiteId,
    string ServiceName,
    string StepName,
    SiteStepHookPhase Phase,
    Func<IServiceProvider, ISiteStepHook> Factory);
