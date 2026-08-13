namespace Muonroi.Logging.Abstractions.Exceptions;

/// <summary>
/// Maps any exception to a single <see cref="ExceptionClassification"/>. The one entry point a
/// host (middleware, background worker, message consumer) calls to route a failure consistently.
/// </summary>
public interface IExceptionClassifier
{
    /// <summary>
    /// Classifies an exception into a structured classification.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    ExceptionClassification Classify(Exception exception);
}
