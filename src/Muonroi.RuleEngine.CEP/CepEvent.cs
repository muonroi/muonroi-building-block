namespace Muonroi.RuleEngine.CEP;

/// <summary>
/// Represents a single event in the CEP engine.
/// </summary>
/// <param name="Key">Correlation key of the event.</param>
/// <param name="Timestamp">Event time in UTC.</param>
/// <param name="Value">Payload carried by the event.</param>
public record CepEvent<T>(string Key, DateTime Timestamp, T Value);
