namespace Muonroi.RuleEngine.Runtime.Tests;

/// <summary>
/// Regression guard for the EF model ↔ migration table-name contract.
///
/// <para>
/// There is NO pluralization / naming convention configured in <see cref="RuleEngineDbContext"/>, so EF Core
/// defaults a table name to the <c>DbSet</c> PROPERTY name. When a DbSet is plural (e.g.
/// <c>OutboundSyncJobs</c>, <c>IngestedSourceDocuments</c>) but its migration + RLS policy created a SINGULAR
/// table (<c>OutboundSyncJob</c>, <c>IngestedSourceDocument</c>), the model targets a non-existent table and
/// every real-Postgres read fails with <c>42P01 relation "…" does not exist</c>. That exact mismatch crash-
/// looped the control-plane host (the <c>OutboundSyncProcessor</c> queries at startup). Each entity must carry
/// an explicit <c>ToTable("…")</c> matching the migration; this test asserts that against the built model.
/// </para>
/// </summary>
public sealed class EntityTableNameTests
{
    private static RuleEngineDbContext BuildContext()
    {
        // UseNpgsql configures the relational provider (so GetTableName resolves) WITHOUT opening a connection —
        // the model is built lazily from metadata only. InMemory is unusable here: it has no relational table names.
        DbContextOptions<RuleEngineDbContext> options = new DbContextOptionsBuilder<RuleEngineDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=none;Password=none")
            .Options;
        return new RuleEngineDbContext(options);
    }

    [Theory]
    [InlineData(typeof(OutboundSyncJobRecord), "OutboundSyncJob")]
    [InlineData(typeof(IngestedSourceDocumentRecord), "IngestedSourceDocument")]
    [InlineData(typeof(CopilotDraftProvenanceRecord), "CopilotDraftProvenance")]
    public void Entity_MapsTo_MigrationTableName(Type clrType, string expectedTable)
    {
        using RuleEngineDbContext db = BuildContext();

        string? actual = db.Model.FindEntityType(clrType)?.GetTableName();

        actual.Should().Be(expectedTable,
            "the EF model must target the singular table the migration + RLS policy created — a plural " +
            "DbSet-derived default 42P01-crashes real-Postgres reads (and the OutboundSyncProcessor at startup)");
    }
}
