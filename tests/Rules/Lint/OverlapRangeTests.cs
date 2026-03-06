namespace Muonroi.Rules.Tests.Lint;

public class OverlapRangeTests
{
    [Fact]
    public void Lint_WarnsOnOverlapRange_ForUniquePolicy()
    {
        const string json = """
{
  "id": "wf1",
  "hitPolicy": "UNIQUE",
  "rules": [
    {
      "id": "r1",
      "range": {"min": 0, "max": 10},
      "outputs": {"value": "A"}
    },
    {
      "id": "r2",
      "range": {"min": 5, "max": 15},
      "outputs": {"value": "B"}
    }
  ]
}
""";

        List<LintMessage> messages = [.. RuleLinter.Lint(json)];

        Assert.Contains(messages, message => message.Code == "LINT_OVERLAP_RANGE");
    }
}
