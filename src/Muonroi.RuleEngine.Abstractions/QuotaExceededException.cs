namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Exception thrown when a tenant exceeds its allocated quota.
/// </summary>
/// <param name="message">The message that describes the error.</param>
public sealed class QuotaExceededException(string message) : Exception(message);
