namespace Muonroi.Core.Abstractions.Interfaces.SiteColumnGroups;

/// <summary>
/// Optional column group for standard audit trail properties.
/// Implement on entities that track creation and modification metadata.
/// No existing code is required to implement this interface.
/// </summary>
public interface IAuditColumns
{
    /// <summary>
    /// The date and time when the record was created.
    /// </summary>
    DateTime? CreatedDate { get; set; }

    /// <summary>
    /// The identifier of the user who created the record.
    /// </summary>
    string? CreatedBy { get; set; }

    /// <summary>
    /// The date and time when the record was last updated.
    /// </summary>
    DateTime? UpdatedDate { get; set; }

    /// <summary>
    /// The identifier of the user who last updated the record.
    /// </summary>
    string? UpdatedBy { get; set; }
}
