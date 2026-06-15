namespace Quickstart.CEP.Api.Models;

/// <summary>
/// Represents an anomaly alert raised by the CEP engine when a sensor's
/// rolling-window average exceeds the configured threshold.
/// </summary>
/// <param name="DeviceId">Device that triggered the alert.</param>
/// <param name="Rule">Human-readable name of the rule that fired.</param>
/// <param name="AverageValue">Average sensor value computed across the window.</param>
/// <param name="EventCount">Number of events included in the window evaluation.</param>
/// <param name="WindowEnd">UTC timestamp at which the window was evaluated.</param>
public sealed record AlertEvent(string DeviceId, string Rule, double AverageValue, int EventCount, DateTime WindowEnd);
