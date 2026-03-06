namespace Muonroi.RuleEngine.Abstractions;

public sealed class QuotaExceededException(string message) : Exception(message);
