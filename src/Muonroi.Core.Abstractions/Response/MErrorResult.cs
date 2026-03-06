namespace Muonroi.Core.Abstractions.Response;

[NotMapped]
public class MErrorResult
{
    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public List<object> ErrorValues { get; set; } = [];

    public override string ToString()
    {
        return ErrorValues is { Count: > 0 }
            ? "[" + ErrorCode + ": " + ErrorMessage + " (" + string.Join(',', ErrorValues) + ")]"
            : "[" + ErrorCode + ": " + ErrorMessage + "]";
    }
}
