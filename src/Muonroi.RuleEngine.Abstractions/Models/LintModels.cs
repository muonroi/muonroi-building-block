namespace Muonroi.RuleEngine.Abstractions.Models;

/// <summary>
/// Defines the severity levels for linting messages.
/// </summary>
public enum LintSeverity
{
    /// <summary>Indicates a potential issue that does not prevent execution.</summary>
    Warning,
    /// <summary>Indicates a critical issue that must be resolved.</summary>
    Error
}

/// <summary>
/// Represents a message generated during the linting process.
/// </summary>
/// <param name="Code">The unique error or warning code.</param>
/// <param name="Message">The descriptive message.</param>
/// <param name="Severity">The severity level of the message.</param>
public record LintMessage(string Code, string Message, LintSeverity Severity);
