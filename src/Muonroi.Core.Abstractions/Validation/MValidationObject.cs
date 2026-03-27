namespace Muonroi.Core.Abstractions.Validation;

/// <summary>
/// Provides a base class for objects that require validation.
/// </summary>
public class MValidationObject
{
    private readonly List<MErrorResult> _errorMessages = [];

    /// <summary>
    /// Gets the collection of validation error messages.
    /// </summary>
    [JsonIgnore] public IReadOnlyCollection<MErrorResult> ErrorMessages => _errorMessages.AsReadOnly();

    /// <summary>
    /// Adds a validation error with a single property value.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="propertyName">The name of the property that failed validation.</param>
    /// <param name="propertyValue">The value of the property.</param>
    protected void AddValidationError(string errorCode, string propertyName, object propertyValue)
    {
        AddValidationError(errorCode, [MHelpers.GenerateErrorResult(propertyName, propertyValue)]);
    }

    /// <summary>
    /// Adds a validation error with a list of error values.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="errorValues">The list of values related to the error.</param>
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

    /// <summary>
    /// Adds multiple validation errors to the current collection.
    /// </summary>
    /// <param name="errorMessages">The collection of error messages to add.</param>
    protected void AddValidationErrors(IEnumerable<MErrorResult> errorMessages)
    {
        _errorMessages.AddRange(errorMessages);
    }

    /// <summary>
    /// Validates the object and populates the ErrorMessages collection.
    /// </summary>
    /// <returns>True if the object is valid; otherwise, false.</returns>
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
