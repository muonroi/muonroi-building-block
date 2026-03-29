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
    Replace = 2
}
