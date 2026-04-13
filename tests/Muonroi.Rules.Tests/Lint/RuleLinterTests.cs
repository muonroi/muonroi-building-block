namespace Muonroi.Rules.Tests.Lint;

public class RuleLinterTests
{
    [Fact]
    public void Lint_InvalidJson_ReturnsError()
    {
        IEnumerable<LintMessage> messages = RuleLinter.Lint("{invalid json");
        messages.Should().ContainSingle(m => m.Code == "LINT_INVALID_JSON");
    }

    [Fact]
    public void Lint_NonObjectRoot_ReturnsError()
    {
        IEnumerable<LintMessage> messages = RuleLinter.Lint("[1,2,3]");
        messages.Should().ContainSingle(m => m.Code == "LINT_INVALID_TYPE");
    }

    [Fact]
    public void Lint_MissingRulesField_ReturnsError()
    {
        string json = "{\"hitPolicy\":\"FIRST\"}";
        IEnumerable<LintMessage> messages = RuleLinter.Lint(json);
        messages.Should().ContainSingle(m => m.Code == "LINT_MISSING_FIELD" && m.Message == "rules");
    }

    [Fact]
    public void Lint_RuleMissingId_ReturnsError()
    {
        string json = "{\"rules\":[{\"outputs\":{\"result\":\"ok\"}}]}";
        IEnumerable<LintMessage> messages = RuleLinter.Lint(json);
        messages.Should().Contain(m => m.Code == "LINT_MISSING_FIELD" && m.Message == "id");
    }

    [Fact]
    public void Lint_DuplicateId_ReturnsError()
    {
        string json = "{\"rules\":[{\"id\":\"r1\",\"outputs\":{\"r\":\"ok\"}},{\"id\":\"r1\",\"outputs\":{\"r\":\"ok\"}}]}";
        IEnumerable<LintMessage> messages = RuleLinter.Lint(json);
        messages.Should().Contain(m => m.Code == "LINT_DUPLICATE_ID" && m.Message == "r1");
    }

    [Fact]
    public void Lint_MissingOutput_ReturnsError()
    {
        string json = "{\"rules\":[{\"id\":\"r1\"}]}";
        IEnumerable<LintMessage> messages = RuleLinter.Lint(json);
        messages.Should().Contain(m => m.Code == "LINT_MISSING_OUTPUT");
    }

    [Fact]
    public void Lint_ValidRule_ReturnsNoErrors()
    {
        string json = "{\"rules\":[{\"id\":\"r1\",\"outputs\":{\"result\":\"ok\"}}]}";
        IEnumerable<LintMessage> messages = RuleLinter.Lint(json);
        messages.Should().BeEmpty();
    }

    [Fact]
    public void Lint_OverlappingRanges_WithFirstPolicy_ReturnsWarning()
    {
        string json = "{\"hitPolicy\":\"FIRST\",\"rules\":[{\"id\":\"r1\",\"outputs\":{\"x\":\"1\"},\"range\":{\"min\":0,\"max\":50}},{\"id\":\"r2\",\"outputs\":{\"x\":\"2\"},\"range\":{\"min\":30,\"max\":80}}]}";
        IEnumerable<LintMessage> messages = RuleLinter.Lint(json);
        messages.Should().Contain(m => m.Code == "LINT_OVERLAP_RANGE");
    }

    [Fact]
    public void Lint_NonObjectRule_ReturnsError()
    {
        string json = "{\"rules\":[\"not_an_object\"]}";
        IEnumerable<LintMessage> messages = RuleLinter.Lint(json);
        messages.Should().Contain(m => m.Code == "LINT_INVALID_TYPE" && m.Message == "rule must be object");
    }
}
