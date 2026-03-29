namespace Muonroi.Core.Abstractions.Interfaces.SiteColumnGroups;

/// <summary>
/// Optional column group for hazardous materials (IMO/DG) container properties.
/// Implement on entities that carry dangerous goods information.
/// No existing code is required to implement this interface.
/// </summary>
public interface IHazardousColumns
{
    /// <summary>
    /// The UN Number identifying the hazardous substance (e.g., "UN1263").
    /// </summary>
    string? UnNo { get; set; }

    /// <summary>
    /// Indicates whether the container carries hazardous goods.
    /// </summary>
    bool? IsHazardous { get; set; }

    /// <summary>
    /// Description of the hazardous content for documentation purposes.
    /// </summary>
    string? HazardousContent { get; set; }
}
