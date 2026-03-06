namespace Muonroi.Core.Abstractions.Response;

public class MVoidMethodResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime UtcTime { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary

    private readonly List<MErrorResult> _errorMessages = [];
    public IReadOnlyCollection<MErrorResult> ErrorMessages => _errorMessages;
    public bool IsOk => _errorMessages.Count == 0;

    public int? StatusCode { get; set; }

    public static string GetErrorMessage(string errorCode, string lang = "vi-VN")
    {
        return MHelpers.GetErrorMessage(errorCode, lang);
    }

    public void AddErrorMessage(MErrorResult errorResult)
    {
        _errorMessages.Add(errorResult);
    }

    public void AddErrorMessage(string errorMessage, string errorCode = "ERROR", string exceptionStackTrace = "")
    {
        AddErrorMessageInternal(errorCode, errorMessage, exceptionStackTrace);
    }

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

    public void AddErrors(IEnumerable<MErrorResult> errors)
    {
        foreach (MErrorResult error in errors)
        {
            AddErrorMessage(error);
        }
    }

    public void AddError(string errorCode)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        AddErrorMessage(errorCode, null, []);
    }

    public void AddError(string errorCode, string lang, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        AddErrorMessage(errorCode, GetErrorMessage(errorCode, lang), arguments);
    }

    public void AddError(string errorCode, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        AddErrorMessage(errorCode, null, arguments);
    }

    public virtual IActionResult GetActionResult()
    {
        ObjectResult objectResult = new(this)
        {
            StatusCode = StatusCode ?? StatusCodes.Status200OK
        };
        return objectResult;
    }

    public static MVoidMethodResult FromError(string errorCode, params string[] arguments)
    {
        MVoidMethodResult result = new();
        result.AddError(errorCode, arguments);
        return result;
    }
}
