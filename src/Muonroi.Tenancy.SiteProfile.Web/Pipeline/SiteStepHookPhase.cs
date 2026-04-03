namespace Muonroi.Tenancy.SiteProfile.Web.Pipeline;

/// <summary>
/// Defines when a hook executes relative to the default step implementation.
/// </summary>
public enum SiteStepHookPhase
{
    /// <summary>
    /// Hook executes BEFORE the default step implementation.
    /// </summary>
    Before = 0,

    /// <summary>
    /// Hook executes AFTER the default step implementation.
    /// </summary>
    After = 1,

    /// <summary>
    /// Hook REPLACES the default step implementation entirely.
    /// When Replace hooks are registered, the defaultImpl is never called.
    /// </summary>
    Replace = 2,

    /// <summary>
    /// Hook WRAPS the default step implementation — receives a <c>next</c> delegate
    /// to optionally invoke the default. Allows modifying input before and output after
    /// the default runs, without copying the entire default implementation.
    /// Requires the hook to implement <see cref="ISiteWrapperHook"/>.
    /// When Wrap hooks are registered, they replace the default impl in the rule set
    /// and receive <c>next</c> pointing to the original default.
    /// </summary>
    Wrap = 3
}
