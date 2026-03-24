using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core;

namespace Muonroi.AuthZ.Authorization;

/// <summary>
/// Adapts <see cref="RuleOrchestrator{TContext}"/> (no IRuleContext constraint) to
/// <see cref="IMRuleOrchestrator{TContext}"/> (requires IRuleContext) for DI resolution.
/// </summary>
internal sealed class AuthorizationRuleOrchestratorAdapter(
    RuleOrchestrator<AuthorizationRuleContext> inner) : IMRuleOrchestrator<AuthorizationRuleContext>
{
    public Task<OrchestratorResult> ExecuteAsync(
        AuthorizationRuleContext context,
        CancellationToken cancellationToken = default)
        => inner.ExecuteWithResultAsync(context, cancellationToken: cancellationToken);
}
