namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.TraceabilityEntities;

/// <summary>
/// Links a <see cref="RequirementRecord"/> to a rule-graph node (or decision-table identifier)
/// in a specific workflow. This is the <c>requirement ↔ rule</c> edge of the traceability matrix.
/// </summary>
public sealed class RuleLinkRecord
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the owning tenant identifier (max 128).</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>Gets or sets the linked <see cref="RequirementRecord.Id"/>.</summary>
    public Guid RequirementId { get; set; }

    /// <summary>Gets or sets the workflow name the linked node belongs to (max 256).</summary>
    public string Workflow { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rule-graph node identifier OR decision-table identifier (max 256)
    /// that satisfies the linked requirement.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>Gets or sets the identifier of the user who created this link.</summary>
    public string CreatedBy { get; set; } = "system";

    /// <summary>Gets or sets the UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
