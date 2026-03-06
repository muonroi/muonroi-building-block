namespace Muonroi.Core.Abstractions.Validation;

public class MValidationObject
{
    private readonly List<MErrorResult> _errorMessages = [];

    [JsonIgnore] public IReadOnlyCollection<MErrorResult> ErrorMessages => _errorMessages.AsReadOnly();

    protected void AddValidationError(string errorCode, string propertyName, object propertyValue)
    {
        AddValidationError(errorCode, [MHelpers.GenerateErrorResult(propertyName, propertyValue)]);
    }

    protected void AddValidationError(string errorCode, List<object> errorValues)
    {
        MErrorResult item = new()
        {
            ErrorCode = errorCode,
            ErrorMessage = MHelpers.GetErrorMessage(errorCode),
            ErrorValues = errorValues
        };
        _errorMessages.Add(item);
    }

    protected void AddValidationErrors(IEnumerable<MErrorResult> errorMessages)
    {
        _errorMessages.AddRange(errorMessages);
    }

    public virtual bool IsValid()
    {
        _errorMessages.Clear();

        List<ValidationResult> validationResults = ValidateByDataAnnotations(this);
        if (validationResults.Count == 0)
        {
            return true;
        }

        ValidationContext context = new(this);
        foreach (ValidationResult vr in validationResults)
        {
            MErrorResult error = BuildErrorResult(vr);
            FillErrorValuesFromMembers(error, vr, context);
            _errorMessages.Add(error);
        }

        return false;
    }

    private static List<ValidationResult> ValidateByDataAnnotations(object instance)
    {
        List<ValidationResult> results = [];
        ValidationContext context = new(instance);

        // validateAllProperties: true
        Validator.TryValidateObject(instance, context, results, true);
        return results;
    }

    private static MErrorResult BuildErrorResult(ValidationResult validationResult)
    {
        string? code = validationResult.ErrorMessage;
        if (string.IsNullOrWhiteSpace(code))
        {
            code = "ValidationError";
        }

        MErrorResult result = new()
        {
            ErrorCode = code,
            ErrorMessage = MHelpers.GetErrorMessage(code)
        };
        return result;
    }

    private static void FillErrorValuesFromMembers(
        MErrorResult error,
        ValidationResult validationResult,
        ValidationContext context)
    {
        foreach (string memberName in validationResult.MemberNames)
        {
            object? value = GetPropertyValue(context, memberName);
            if (value is null)
            {
                continue;
            }

            error.ErrorValues.Add(MHelpers.GenerateErrorResult(memberName, value));
        }
    }

    private static object? GetPropertyValue(ValidationContext context, string memberName)
    {
        PropertyInfo? prop = context.ObjectType.GetProperty(memberName);
        return prop?.GetValue(context.ObjectInstance, null);
    }
}
