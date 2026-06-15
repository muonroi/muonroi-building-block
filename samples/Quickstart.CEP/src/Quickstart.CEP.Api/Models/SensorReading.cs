namespace Quickstart.CEP.Api.Models;

/// <summary>
/// A telemetry reading emitted by an IoT sensor device.
/// </summary>
/// <param name="DeviceId">Unique identifier of the device that produced the reading.</param>
/// <param name="Metric">Name of the measured quantity (e.g. "temperature", "humidity").</param>
/// <param name="Value">Measured numeric value.</param>
/// <param name="Timestamp">Wall-clock time when the sensor captured the reading.</param>
public sealed record SensorReading(string DeviceId, string Metric, double Value, DateTime Timestamp);
