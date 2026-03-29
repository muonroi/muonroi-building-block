namespace Muonroi.Core.Abstractions.Interfaces.SiteColumnGroups;

/// <summary>
/// Optional column group for vessel and voyage properties.
/// Implement on entities that track the carrying vessel and outbound voyage.
/// No existing code is required to implement this interface.
/// </summary>
public interface IVesselColumns
{
    /// <summary>
    /// The unique vessel code or IMO number.
    /// </summary>
    string? VesselCode { get; set; }

    /// <summary>
    /// The human-readable vessel name.
    /// </summary>
    string? VesselName { get; set; }

    /// <summary>
    /// The outbound voyage number.
    /// </summary>
    string? OutVoyage { get; set; }
}
