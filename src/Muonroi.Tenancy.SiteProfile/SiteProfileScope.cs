using System.Threading;

namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Temporarily overrides the resolved site profile for the current async context.
/// Use in tests and background jobs where ISiteProfileResolver is not available.
///
/// <code>
/// using (SiteProfileScope.ForSite(myProfile))
/// {
///     // ISiteProfileResolver.Current returns myProfile here
/// }
/// </code>
///
/// Follows the ContextMirrorScope push/pop pattern from Muonroi.Tenancy.Core.
/// The override is propagated via <see cref="AsyncLocal{T}"/> — it is visible in
/// child async tasks but not in sibling or parent async contexts.
/// </summary>
public sealed class SiteProfileScope : IDisposable
{
    private static readonly AsyncLocal<ISiteProfile?> s_current = new();
    private readonly ISiteProfile? _previous;
    private bool _disposed;

    private SiteProfileScope(ISiteProfile profile)
    {
        _previous = s_current.Value;
        s_current.Value = profile;
    }

    /// <summary>
    /// Gets the current scope-overridden profile, or null if no scope is active.
    /// Called by ISiteProfileResolver factory to check for active scope override.
    /// </summary>
    internal static ISiteProfile? Current => s_current.Value;

    /// <summary>
    /// Creates a scope that overrides the resolved site profile for the current async context.
    /// Dispose the returned scope to restore the previous profile.
    /// </summary>
    /// <param name="profile">The site profile to use within the scope.</param>
    /// <returns>A disposable scope that restores the previous profile on dispose.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
    public static SiteProfileScope ForSite(ISiteProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new SiteProfileScope(profile);
    }

    /// <summary>
    /// Restores the previous site profile on the async context.
    /// Idempotent — safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        s_current.Value = _previous;
        _disposed = true;
    }
}
