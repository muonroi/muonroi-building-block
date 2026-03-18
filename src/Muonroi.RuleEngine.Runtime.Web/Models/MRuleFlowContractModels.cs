namespace Muonroi.RuleEngine.Runtime.Web.Models;

/// <summary>
/// Represents a single field in a rule contract schema.
/// </summary>
public sealed record MRuleContractField(
    string Name,
    string Type,
    bool IsRequired = false,
    string? Description = null,
    IReadOnlyList<MRuleContractField>? Children = null
);

/// <summary>
/// Represents a contract schema (input or output) for a rule or flow.
/// </summary>
public sealed record MRuleContractSchema(
    string ContractName,
    IReadOnlyList<MRuleContractField> Fields
);

/// <summary>
/// Response for rule/flow contract lookup.
/// </summary>
public sealed record MRuleFlowContractLookupResponse(
    string SourceType,
    string SourceCode,
    MRuleContractSchema? RequestContract,
    MRuleContractSchema? ResponseContract
);

/// <summary>
/// Response for node authoring contract lookup.
/// </summary>
public sealed record MRuleFlowNodeContractResponse(
    string NodeId,
    string FlowCode,
    MRuleContractSchema? RequestScope,
    MRuleContractSchema? ResponseDelta,
    IReadOnlyList<string>? AvailableInputKeys = null
);

/// <summary>
/// Summary of a rule flow (used for listing).
/// </summary>
public sealed record MRuleFlowSummary(
    string Code,
    string Name,
    int? ActiveVersion,
    int TotalVersions
);
