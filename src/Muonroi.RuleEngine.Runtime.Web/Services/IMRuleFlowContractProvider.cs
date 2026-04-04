using Muonroi.RuleEngine.Runtime.Web.Models;

namespace Muonroi.RuleEngine.Runtime.Web.Services;

/// <summary>
/// Provides rule and flow contract schemas for the flow designer.
/// Consumers can override this to customize how contracts are resolved.
/// </summary>
public interface IMRuleFlowContractProvider
{
    /// <summary>
    /// Get the I/O contract for a rule or flow by source type and code.
    /// </summary>
    Task<MRuleFlowContractLookupResponse?> MGetContractAsync(
        string sourceType, string sourceCode, CancellationToken ct = default);

    /// <summary>
    /// Get the flow-level contract for a workflow.
    /// </summary>
    Task<MRuleFlowContractLookupResponse?> MGetFlowContractAsync(
        string flowCode, CancellationToken ct = default);

    /// <summary>
    /// Get the authoring contract for a specific node in a flow.
    /// </summary>
    Task<MRuleFlowNodeContractResponse?> MGetNodeAuthoringContractAsync(
        string flowCode, string nodeId, CancellationToken ct = default);

    /// <summary>
    /// List all available rule flows/rulesets.
    /// </summary>
    Task<IReadOnlyList<MRuleFlowSummary>> MListFlowsAsync(CancellationToken ct = default);
}
