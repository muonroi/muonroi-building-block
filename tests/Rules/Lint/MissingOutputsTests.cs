namespace Muonroi.Rules.Tests.Lint;

public class MissingOutputsTests
{
    [Fact]
    public void Lint_ReturnsMissingOutputCode_WhenOutputsMissing()
    {
        const string json = """
{
  "id": "wf1",
  "hitPolicy": "FIRST",
  "rules": [
    {
      "id": "r1",
      "range": {"min": 0, "max": 10}
    }
  ]
}
""";

        List<LintMessage> messages = [.. RuleLinter.Lint(json)];

        Assert.Contains(messages, message => message.Code == "LINT_MISSING_OUTPUT");
    }
}
