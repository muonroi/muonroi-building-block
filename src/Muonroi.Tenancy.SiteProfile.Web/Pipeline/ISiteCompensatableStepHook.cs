namespace Muonroi.Tenancy.SiteProfile.Web.Pipeline;

/// <summary>
/// Extends <see cref="ISiteStepHook"/> with compensation support.
/// When a pipeline step uses <see cref="ExecutionMode.CompensateOnFailure"/>,
/// hooks implementing this interface will have <see cref="CompensateAsync"/> called in LIFO order
/// if a subsequent hook or step fails.
/// </summary>
public interface ISiteCompensatableStepHook : ISiteStepHook
{
    /// <summary>
    /// Attempts to undo or mitigate side effects produced by <see cref="ISiteStepHook.ExecuteAsync"/>.
    /// Implementations MUST NOT throw exceptions — errors should be handled internally.
    /// Compensation should be idempotent because it may be invoked multiple times.
    /// </summary>
    /// <param name="facts">The shared fact bag for this pipeline execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CompensateAsync(FactBag facts, CancellationToken cancellationToken = default);
}
