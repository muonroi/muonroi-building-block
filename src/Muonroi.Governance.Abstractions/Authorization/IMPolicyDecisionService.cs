namespace Muonroi.Governance.Authorization;

/// <summary>
/// Defines the policy decision service contract.
/// </summary>
public interface IMPolicyDecisionService
{
    /// <summary>
    /// Gets a value indicating whether policy decisions are enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Evaluates a policy decision request.
    /// </summary>
    Task<MPolicyDecisionResult> EvaluateAsync(MPolicyDecisionRequest request, CancellationToken cancellationToken = default);
}
