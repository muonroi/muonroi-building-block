namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Query parameters for listing decision tables.
/// </summary>
public sealed class DecisionTableQuery
{
    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
    /// <summary>Page size.</summary>
    public int PageSize { get; set; } = 20;
    /// <summary>Optional search term.</summary>
    public string? Search { get; set; }
    /// <summary>Optional tenant filter.</summary>
    public string? TenantId { get; set; }
    /// <summary>Optional hit policy filter.</summary>
    public HitPolicy? HitPolicy { get; set; }
    /// <summary>Whether deleted tables should be included.</summary>
    public bool IncludeDeleted { get; set; }
}
