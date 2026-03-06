namespace Muonroi.Core.Abstractions.Response;

public class MResponse<T> : MVoidMethodResult
{
    public T? Result { get; set; }
    public MErrorResult? Error { get; set; }

    public new void AddErrors(IEnumerable<MErrorResult> errorMessages)
    {
        foreach (MErrorResult errorMessage in errorMessages)
        {
            AddErrorMessage(errorMessage);
        }
    }

    public void SetError(string errorCode)
    {
        SetError(errorCode, []);
    }

    public void SetError(string errorCode, params object[] arguments)
    {
        SetErrorMessage(errorCode, null, arguments);
    }

    public void SetError(string errorCode, string lang, params object[] arguments)
    {
        SetErrorMessage(errorCode, MHelpers.GetErrorMessage(errorCode, lang), arguments);
    }

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

    public override IActionResult GetActionResult()
    {
        ObjectResult objectResult = new(this);
        if (StatusCode.HasValue)
        {
            objectResult.StatusCode = StatusCode;
            return objectResult;
        }

        objectResult.StatusCode = StatusCodes.Status200OK
            ;

        return objectResult;
    }
}
