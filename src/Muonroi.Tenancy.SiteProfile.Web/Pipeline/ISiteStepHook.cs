using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Tenancy.SiteProfile.Web.Pipeline;

/// <summary>
/// Defines a hook that intercepts a named pipeline step.
/// Hooks are resolved per-request and receive the shared FactBag.
/// </summary>
public interface ISiteStepHook
{
    /// <summary>
    /// Executes the hook logic.
    /// The <paramref name="facts"/> bag is shared with the default step implementation
    /// and all other hooks — mutations are visible across the pipeline.
    /// </summary>
    /// <param name="facts">The shared fact bag for this pipeline execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteAsync(FactBag facts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optional execution order within a phase. Lower values execute first.
    /// Default is 0. Override to control ordering when multiple hooks target the same step and phase.
    /// </summary>
    int Order => 0;

    /// <summary>
    /// Optional list of hook codes that must execute before this hook.
    /// Default is empty. Override to express inter-hook dependencies within the same pipeline step.
    /// </summary>
    IReadOnlyList<string> DependsOn => [];
}
