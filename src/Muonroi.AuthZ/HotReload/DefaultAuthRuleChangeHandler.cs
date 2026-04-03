namespace Muonroi.AuthZ.HotReload;

using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Default implementation of <see cref="IAuthRuleChangeHandler"/> that invalidates the
/// rule-engine runtime cache when authorization rules change via hot-reload.
/// Automatically registered by <c>AddMAuthorizationRuleEngine()</c> via
/// <c>TryAddSingleton</c> — consumer apps may override by registering their own
/// implementation before calling that extension.
/// </summary>
public sealed class DefaultAuthRuleChangeHandler(
    IRuleSetRuntimeCache? runtimeCache = null,
    IMLog<DefaultAuthRuleChangeHandler>? logger = null) : IAuthRuleChangeHandler
{
    /// <inheritdoc />
    public async Task OnAuthRuleChangedAsync(Guid ruleSetId, CancellationToken ct = default)
    {
        logger?.Info("[AuthZ] Auth rule changed: {RuleSetId}. Invalidating authorization caches.", ruleSetId);

        if (runtimeCache is not null)
        {
            // Invalidate auth workflow entries for the global (empty) tenant scope.
            // The tenantId is passed as an empty string to signal cross-tenant invalidation;
            // implementations that scope by tenant will no-op on keys that do not match.
            await runtimeCache.InvalidateAsync(string.Empty, "auth/", ct);
        }
    }
}
