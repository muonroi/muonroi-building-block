namespace Muonroi.RuleEngine.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a rule code maps to multiple rule implementations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RuleEngineAmbiguousCodeException"/> class.
/// </remarks>
/// <param name="message">The human-readable error message.</param>
public sealed class RuleEngineAmbiguousCodeException(string message) : MConfigurationException(message, "RuleEngine:AmbiguousCode")
{
}
