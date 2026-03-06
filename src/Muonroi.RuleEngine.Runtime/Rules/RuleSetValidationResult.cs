namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class RuleSetValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public string WorkflowName { get; set; } = string.Empty;
    public string Shape { get; set; } = "Unknown";
    public List<RuleSetValidationIssue> Errors { get; } = [];
    public List<RuleSetValidationIssue> Warnings { get; } = [];

    public void ThrowIfInvalid()
    {
        if (IsValid)
        {
            return;
        }

        string message = string.Join("; ", Errors.Select(x => x.Message));
        throw new InvalidDataException(message);
    }
}
