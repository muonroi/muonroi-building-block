namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Record representing a specific validation error on a field.
/// </summary>
/// <param name="Field">The field name that failed validation.</param>
/// <param name="Message">The human-readable validation error message.</param>
/// <param name="AttemptedValue">The value that failed validation.</param>
public record MValidationError(string Field, string Message, object? AttemptedValue = null);

/// <summary>
/// Exception thrown when input data validation fails.
/// </summary>
public sealed class MValidationException : MException
{
    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public IReadOnlyList<MValidationError> Errors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MValidationException"/> class with a collection of errors.
    /// </summary>
    /// <param name="errors">The collection of validation errors.</param>
    /// <param name="message">The optional error message.</param>
    public MValidationException(IEnumerable<MValidationError> errors, string? message = null)
        : base("VALIDATION_FAILED", message ?? "One or more validation errors occurred.", MExceptionCategory.Validation, 400)
    {
        Errors = errors.ToList().AsReadOnly();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MValidationException"/> class for a single field error.
    /// </summary>
    /// <param name="field">The field name that failed validation.</param>
    /// <param name="message">The validation error message.</param>
    public MValidationException(string field, string message)
        : this(new[] { new MValidationError(field, message) })
    {
    }
}
