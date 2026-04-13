using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.RuleEngine.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a rule code maps to multiple rule implementations.
/// </summary>
public sealed class RuleEngineAmbiguousCodeException : MConfigurationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RuleEngineAmbiguousCodeException"/> class.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    public RuleEngineAmbiguousCodeException(string message)
        : base(message, "RuleEngine:AmbiguousCode")
    {
    }
}
