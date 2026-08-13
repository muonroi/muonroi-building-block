using System;
using Muonroi.Logging.Abstractions.Exceptions;

namespace Muonroi.Logging.Exceptions;

/// <summary>
/// A default implementation of <see cref="IExceptionClassifier"/> that maps
/// common exception types to standardized error codes and retryable statuses.
/// </summary>
public sealed class DefaultExceptionClassifier : IExceptionClassifier
{
    /// <inheritdoc />
    public ExceptionClassification Classify(Exception exception)
    {
        if (exception == null)
            return ExceptionClassification.Unknown(new Exception("Unknown"));

        string typeName = exception.GetType().Name;
        string fullName = exception.GetType().FullName ?? string.Empty;

        // 1. Network / Timeout exceptions (Retryable)
        if (typeName.Contains("Timeout") || typeName.Contains("Socket") || typeName.Contains("HttpRequest"))
        {
            return new ExceptionClassification("INF-NET-0001", true, "A temporary network error occurred. Please try again.");
        }

        // 2. Database / EF Core / SQL exceptions
        if (fullName.Contains("Sql") || fullName.Contains("DbUpdate") || fullName.Contains("Postgres") || fullName.Contains("MySql"))
        {
            // E.g. DbUpdateConcurrencyException might be retryable
            bool isRetryable = typeName.Contains("Concurrency") || typeName.Contains("Transient");
            return new ExceptionClassification("INF-DB-0001", isRetryable, "A database operation failed.");
        }

        // 3. Redis / Caching exceptions
        if (fullName.Contains("Redis") || typeName.Contains("Cache"))
        {
            return new ExceptionClassification("INF-CACHE-0001", true, "A temporary caching error occurred.");
        }

        // 4. Validation / Argument exceptions (Not Retryable)
        if (typeName.Contains("Argument") || typeName.Contains("Validation"))
        {
            return new ExceptionClassification("APP-VAL-0001", false, "Invalid input provided.");
        }

        // 5. Authentication / Authorization
        if (typeName.Contains("Unauthorized") || typeName.Contains("Security"))
        {
            return new ExceptionClassification("SEC-AUTH-0001", false, "You do not have permission to perform this action.");
        }

        return ExceptionClassification.Unknown(exception);
    }
}
