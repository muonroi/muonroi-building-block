using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Core.Abstractions.Response;

/// <summary>
/// Represents the result of a method that does not return a value.
/// </summary>
public class MVoidMethodResult
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the UTC time.
    /// </summary>
    public DateTime UtcTime { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary

    private readonly List<MErrorResult> _errorMessages = [];

    /// <summary>
    /// Gets the collection of error messages.
    /// </summary>
    public IReadOnlyCollection<MErrorResult> ErrorMessages => _errorMessages;

    /// <summary>
    /// Gets a value indicating whether the result is successful.
    /// </summary>
    public bool IsOk => _errorMessages.Count == 0;

    /// <summary>
    /// Gets or sets the HTTP status code.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Gets the error message for the specified error code and language.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="lang">The language code.</param>
    /// <returns>The error message string.</returns>
    public static string GetErrorMessage(string errorCode, string lang = "vi-VN")
    {
        return MHelpers.GetErrorMessage(errorCode, lang);
    }

    /// <summary>
    /// Adds an error result message.
    /// </summary>
    /// <param name="errorResult">The error result.</param>
    public void AddErrorMessage(MErrorResult errorResult)
    {
        _errorMessages.Add(errorResult);
    }

    /// <summary>
    /// Adds an error message with the specified message and error code.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="errorCode">The error code.</param>
    /// <param name="exceptionStackTrace">The exception stack trace.</param>
    public void AddErrorMessage(string errorMessage, string errorCode = "ERROR", string exceptionStackTrace = "")
    {
        AddErrorMessageInternal(errorCode, errorMessage, exceptionStackTrace);
    }

    /// <summary>
    /// Adds an error message with the specified error code, message, and arguments.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="arguments">The arguments.</param>
    public void AddErrorMessage(string errorCode, string? errorMessage, params string[] arguments)
    {
        string resolvedMessage = string.IsNullOrEmpty(errorMessage) ? GetErrorMessage(errorCode) : errorMessage;

        MErrorResult errorResult = new()
        {
            ErrorCode = errorCode,
            ErrorMessage = resolvedMessage
        };

        if (arguments.Length > 0)
        {
            errorResult.ErrorValues.AddRange(arguments);
        }

        AddErrorMessage(errorResult);
    }

    private void AddErrorMessageInternal(
        string errorCode,
        string errorMessage,
        string exceptionStackTrace,
        params object[] arguments)
    {
        _ = exceptionStackTrace;

        MErrorResult item = new()
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ErrorValues = [.. arguments]
        };
        _errorMessages.Add(item);
    }

    /// <summary>
    /// Adds a collection of error results.
    /// </summary>
    /// <param name="errors">The collection of error results.</param>
    public void AddErrors(IEnumerable<MErrorResult> errors)
    {
        foreach (MErrorResult error in errors)
        {
            AddErrorMessage(error);
        }
    }

    /// <summary>
    /// Adds an error with the specified error code.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    public void AddError(string errorCode)
    {
        MGuard.NotNull(errorCode);
        AddErrorMessage(errorCode, null, []);
    }

    /// <summary>
    /// Adds an error with the specified error code, language, and arguments.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="lang">The language code.</param>
    /// <param name="arguments">The arguments.</param>
    public void AddError(string errorCode, string lang, params string[] arguments)
    {
        MGuard.NotNull(errorCode);
        AddErrorMessage(errorCode, GetErrorMessage(errorCode, lang), arguments);
    }

    /// <summary>
    /// Adds an error with the specified error code and arguments.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="arguments">The arguments.</param>
    public void AddError(string errorCode, params string[] arguments)
    {
        MGuard.NotNull(errorCode);
        AddErrorMessage(errorCode, null, arguments);
    }

    /// <summary>
    /// Returns an action result based on the result.
    /// </summary>
    /// <returns>The action result.</returns>
    public virtual IActionResult GetActionResult()
    {
        ObjectResult objectResult = new(this)
        {
            StatusCode = StatusCode ?? StatusCodes.Status200OK
        };
        return objectResult;
    }

    /// <summary>
    /// Creates a result from an error code and arguments.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The method result.</returns>
    public static MVoidMethodResult FromError(string errorCode, params string[] arguments)
    {
        MVoidMethodResult result = new();
        result.AddError(errorCode, arguments);
        return result;
    }
}
