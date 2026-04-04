using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Quota.Abstractions;

/// <summary>
/// Exception thrown when a tenant exceeds its allocated quota.
/// </summary>
/// <param name="message">The message that describes the error.</param>
public sealed class QuotaExceededException(string message)
    : MException("QUOTA_EXCEEDED", message, MExceptionCategory.Domain, 429);
