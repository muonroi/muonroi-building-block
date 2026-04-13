namespace Muonroi.Rules.Tests.Table;

public class DecisionTableImportExportTests
{
    [Fact]
    public void Import_ShouldReturnRulesAndHitPolicy()
    {
        string csv = @"HitPolicy,FIRST
Age,Result
[0..17],Minor
[18..65],Adult
[66..120],Senior";
        DecisionTable table = DecisionTableImporter.ImportCsv(csv);
        Assert.Equal(HitPolicy.First, table.HitPolicy);
        Assert.Equal(3, table.Rules.Count);

        string exported = DecisionTableExporter.ExportCsv(table);
        Assert.Contains("HitPolicy,First", exported, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FeelEvaluator_ShouldHandleRange()
    {
        bool ok = FeelEvaluator.Evaluate("age in [18..65]", new Dictionary<string, object> { ["age"] = 30 });
        bool fail = FeelEvaluator.Evaluate("age in [18..65]", new Dictionary<string, object> { ["age"] = 70 });

        Assert.True(ok);
        Assert.False(fail);
    }

    [Fact]
    public void Import_ShouldWarnOnOverlap()
    {
        string csv = @"HitPolicy,FIRST
Age,Result
[18..65],Adult
[60..80],Senior";
        DecisionTable table = DecisionTableImporter.ImportCsv(csv);
        Assert.Single(table.Warnings);
    }
}
