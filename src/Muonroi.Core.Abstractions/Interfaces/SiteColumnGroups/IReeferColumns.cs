namespace Muonroi.Core.Abstractions.Interfaces.SiteColumnGroups;

/// <summary>
/// Optional column group for reefer (temperature-controlled) container properties.
/// Implement on entities that have reefer-related columns.
/// Used as a composition marker for site-specific entity configurations.
/// No existing code is required to implement this interface.
/// </summary>
public interface IReeferColumns
{
    /// <summary>
    /// The required set-point temperature for the reefer container.
    /// </summary>
    decimal? Temperature { get; set; }

    /// <summary>
    /// The temperature unit (e.g., "C" for Celsius, "F" for Fahrenheit).
    /// </summary>
    string? TemperatureUnit { get; set; }

    /// <summary>
    /// The ventilation rate (air exchanges per hour).
    /// </summary>
    decimal? Ventilation { get; set; }

    /// <summary>
    /// The relative humidity set-point percentage.
    /// </summary>
    decimal? Humidity { get; set; }
}
