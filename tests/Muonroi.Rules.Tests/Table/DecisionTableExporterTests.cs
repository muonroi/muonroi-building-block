namespace Muonroi.Rules.Tests.Table;

public class DecisionTableExporterTests
{
    [Fact]
    public void ExportCsv_ValidTable_ProducesCorrectCsv()
    {
        var table = new DecisionTable { HitPolicy = HitPolicy.First };
        table.InputHeaders.AddRange(new[] { "age", "score" });
        table.OutputHeaders.Add("result");
        table.Rules.Add(new DecisionRule(
            new Dictionary<string, string> { ["age"] = ">18", ["score"] = ">50" },
            new Dictionary<string, string> { ["result"] = "pass" }));

        string csv = DecisionTableExporter.ExportCsv(table);

        csv.Should().Contain("HitPolicy,First");
        csv.Should().Contain("age,score,result");
        csv.Should().Contain(">18,>50,pass");
    }

    [Fact]
    public void ExportCsv_MultipleRules_ProducesMultipleRows()
    {
        var table = new DecisionTable { HitPolicy = HitPolicy.Unique };
        table.InputHeaders.Add("x");
        table.OutputHeaders.Add("y");
        table.Rules.Add(new DecisionRule(
            new Dictionary<string, string> { ["x"] = "1" },
            new Dictionary<string, string> { ["y"] = "a" }));
        table.Rules.Add(new DecisionRule(
            new Dictionary<string, string> { ["x"] = "2" },
            new Dictionary<string, string> { ["y"] = "b" }));

        string csv = DecisionTableExporter.ExportCsv(table);
        string[] lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(4);
    }

    [Fact]
    public void ExportCsv_EmptyRules_HasOnlyHeaders()
    {
        var table = new DecisionTable { HitPolicy = HitPolicy.First };
        table.InputHeaders.Add("x");
        table.OutputHeaders.Add("y");

        string csv = DecisionTableExporter.ExportCsv(table);
        string[] lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
    }
}
