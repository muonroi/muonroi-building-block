namespace Muonroi.Governance.Authorization;

public interface IMPolicyDecisionService
{
    bool IsEnabled { get; }
    Task<MPolicyDecisionResult> EvaluateAsync(MPolicyDecisionRequest request, CancellationToken cancellationToken = default);
}
