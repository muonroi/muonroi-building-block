namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules.TraceabilityEntities;

/// <summary>
/// Discriminates the kind of test that a <see cref="TestLinkRecord"/> represents.
/// <para>
/// The distinction is load-bearing for the traceability matrix test column: a stored dry-run
/// example is NOT real coverage, and the matrix must never collapse the two (PROJECT honesty
/// constraint C-05 / decision D-03).
/// </para>
/// </summary>
public enum TestLinkKind
{
    /// <summary>A stored dry-run example case (illustrative, NOT real unit-test coverage).</summary>
    DryRunExample,

    /// <summary>A real unit test linked via the <c>[MExtractAsRule("CODE")]</c> convention.</summary>
    UnitTest
}

/// <summary>
/// Links a rule-graph node to a test case. This is the <c>rule ↔ test</c> edge of the
/// traceability matrix. <see cref="Kind"/> records whether the link is real unit-test coverage
/// or merely a stored dry-run example (decision D-03).
/// </summary>
public sealed class TestLinkRecord
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the owning tenant identifier (max 128).</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>Gets or sets the workflow name the linked node belongs to (max 256).</summary>
    public string Workflow { get; set; } = string.Empty;

    /// <summary>Gets or sets the rule-graph node identifier (max 256).</summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the case identifier (max 256): the <c>[MExtractAsRule]</c> CODE for a
    /// <see cref="TestLinkKind.UnitTest"/>, or the <see cref="DryRunExampleRecord.Id"/> for a
    /// <see cref="TestLinkKind.DryRunExample"/>.
    /// </summary>
    public string CaseId { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind of test this link represents (decision D-03).</summary>
    public TestLinkKind Kind { get; set; }

    /// <summary>Gets or sets the UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
