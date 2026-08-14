namespace Muonroi.Tenancy.SiteProfile.Web.Pipeline;

/// <summary>
/// A hook that WRAPS the default step implementation — can call, skip, or modify
/// the default behavior without copying it entirely.
///
/// Use when a site needs to change 5-20% of the default logic.
/// <list type="bullet">
/// <item><description><see cref="SiteStepHookPhase.Before"/>/<see cref="SiteStepHookPhase.After"/>: cannot modify DURING default execution</description></item>
/// <item><description><see cref="SiteStepHookPhase.Replace"/>: must copy 100% of default logic</description></item>
/// <item><description><see cref="SiteStepHookPhase.Wrap"/>: modify input → call default → modify output</description></item>
/// </list>
///
/// <example>
/// <code>
/// public class CtlCustomsWrapHook : ISiteWrapperHook
/// {
///     public async Task WrapAsync(FactBag facts, Func&lt;FactBag, CancellationToken, Task&gt; next, CancellationToken ct)
///     {
///         // Pre: override customs input
///         facts.Set("customs.zone", "CTL_ZONE_B");
///
///         await next(facts, ct);  // call default (100 lines of mapping logic)
///
///         // Post: patch 2 fields
///         facts.Set("mapped.CustomsReq", 2);
///     }
/// }
/// </code>
/// </example>
/// </summary>
public interface ISiteWrapperHook : ISiteStepHook
{
    /// <summary>
    /// Wraps the default step implementation.
    /// Call <paramref name="next"/> to execute the default, or skip it entirely.
    /// </summary>
    /// <param name="facts">The shared fact bag.</param>
    /// <param name="next">The default step implementation (or the next wrapper in chain).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WrapAsync(FactBag facts, Func<FactBag, CancellationToken, Task> next, CancellationToken cancellationToken = default);
}
