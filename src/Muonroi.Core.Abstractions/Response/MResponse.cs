namespace Muonroi.Core.Abstractions.Response;

/// <summary>
/// Represents a response with a result of type T.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
public class MResponse<T> : MVoidMethodResult
{
    /// <summary>
    /// Gets or sets the result.
    /// </summary>
    public T? Result { get; set; }

    /// <summary>
    /// Gets or sets the error result.
    /// </summary>
    public MErrorResult? Error { get; set; }

    /// <summary>
    /// Adds a collection of error results.
    /// </summary>
    /// <param name="errorMessages">The collection of error results.</param>
    public new void AddErrors(IEnumerable<MErrorResult> errorMessages)
    {
        foreach (MErrorResult errorMessage in errorMessages)
        {
            AddErrorMessage(errorMessage);
        }
    }

    /// <summary>
    /// Sets an error with the specified error code.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    public void SetError(string errorCode)
    {
        SetError(errorCode, Array.Empty<object>());
    }

    /// <summary>
    /// Sets an error with the specified error code and arguments.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="arguments">The arguments.</param>
    public void SetError(string errorCode, params object[] arguments)
    {
        SetErrorMessage(errorCode, null, arguments);
    }

    /// <summary>
    /// Sets an error with the specified error code, language, and arguments.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="lang">The language code.</param>
    /// <param name="arguments">The arguments.</param>
    public void SetError(string errorCode, string lang, params object[] arguments)
    {
        SetErrorMessage(errorCode, MHelpers.GetErrorMessage(errorCode, lang), arguments);
    }

    /// <summary>
    /// Sets the error message with the specified error code, message, and arguments.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="arguments">The arguments.</param>
    public void SetErrorMessage(string errorCode, string? errorMessage, params object[]? arguments)
    {
        MErrorResult error = new()
        {
            ErrorCode = errorCode,
            ErrorMessage = string.IsNullOrEmpty(errorMessage) ? GetErrorMessage(errorCode) : errorMessage
        };
        if (arguments != null && arguments.Length != 0)
        {
            error.ErrorValues.AddRange(arguments);
        }

        Error = error;
        AddErrorMessage(error);
    }

    /// <summary>
    /// Returns an action result based on the response.
    /// </summary>
    /// <returns>The action result.</returns>
    public override IActionResult GetActionResult()
    {
        ObjectResult objectResult = new(this);
        if (StatusCode.HasValue)
        {
            objectResult.StatusCode = StatusCode;
            return objectResult;
        }

        objectResult.StatusCode = StatusCodes.Status200OK;

        return objectResult;
    }
}
