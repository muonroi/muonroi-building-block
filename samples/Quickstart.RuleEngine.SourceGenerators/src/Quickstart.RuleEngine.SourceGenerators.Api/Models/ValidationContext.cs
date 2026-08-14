namespace Quickstart.RuleEngine.SourceGenerators.Api.Models;

public class ValidationContext : IRuleContext
{
    public string InputText { get; set; } = string.Empty;
    public int Age { get; set; }
    public void HaltGroup() { }
}
