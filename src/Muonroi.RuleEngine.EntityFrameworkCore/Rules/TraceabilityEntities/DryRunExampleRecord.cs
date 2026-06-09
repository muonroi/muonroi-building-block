namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.TraceabilityEntities;

/// <summary>
/// A dry-run input promoted into a stored example case for a rule-graph node. Seeds the
/// traceability matrix test column with an illustrative (NOT unit-test) case.
/// <para>
/// Promotion is a manual one-click action guarded by <c>living-docs:write</c> (decision D-02),
/// NOT auto-capture of every dry-run. The persisted <see cref="InputsJson"/> mirrors the
/// <c>DryRunRuleSetRequest.Inputs</c> shape.
/// </para>
/// </summary>
public sealed class DryRunExampleRecord
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the owning tenant identifier (max 128).</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>Gets or sets the workflow name the example belongs to (max 256).</summary>
    public string Workflow { get; set; } = string.Empty;

    /// <summary>Gets or sets the rule-graph node identifier the example exercises (max 256).</summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>Gets or sets the ruleset version the example was promoted against.</summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the serialized dry-run inputs (JSON object). Mirrors
    /// <c>DryRunRuleSetRequest.Inputs</c>. Defaults to an empty object.
    /// </summary>
    public string InputsJson { get; set; } = "{}";

    /// <summary>Gets or sets the optional dry-run context type (max 256).</summary>
    public string? ContextType { get; set; }

    /// <summary>Gets or sets the identifier of the user who promoted this example.</summary>
    public string PromotedBy { get; set; } = "system";

    /// <summary>Gets or sets the UTC promotion timestamp.</summary>
    public DateTimeOffset PromotedAt { get; set; } = DateTimeOffset.UtcNow;
}
