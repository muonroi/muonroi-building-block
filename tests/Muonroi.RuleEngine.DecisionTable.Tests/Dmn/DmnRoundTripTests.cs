using Muonroi.RuleEngine.DecisionTable.Dmn;
using Muonroi.RuleEngine.DecisionTable.Models;
using DecisionTableModel = Muonroi.RuleEngine.DecisionTable.Models.DecisionTable;

namespace Muonroi.RuleEngine.DecisionTable.Tests.Dmn;

/// <summary>
/// Round-trip tests — export a DecisionTableModel to DMN XML then import back,
/// verifying semantic equivalence (column names, cell expressions, hit policy).
/// IDs may differ between original and imported table since DMN IDs are regenerated.
/// </summary>
public sealed class DmnRoundTripTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static DmnImportResult RoundTrip(DecisionTableModel table)
    {
        string xml = DmnExporter.ExportToDmnXml(table);
        return DmnImporter.ImportFromDmnXml(xml);
    }

    private static DecisionTableModel BuildFullTable(HitPolicy hitPolicy = HitPolicy.First) => new()
    {
        Id = "rt-table",
        Name = "Round Trip Table",
        HitPolicy = hitPolicy,
        InputColumns =
        [
            new DecisionTableColumn { Id = "in-age",    Name = "applicantAge",  Label = "Age",     DataType = "number" },
            new DecisionTableColumn { Id = "in-income", Name = "monthlyIncome", Label = "Income",  DataType = "number" },
            new DecisionTableColumn { Id = "in-status", Name = "customerStatus",Label = "Status",  DataType = "string" }
        ],
        OutputColumns =
        [
            new DecisionTableColumn { Id = "out-risk",   Name = "riskCategory",  Label = "Risk",    DataType = "string" },
            new DecisionTableColumn { Id = "out-factor", Name = "riskFactor",    Label = "Factor",  DataType = "number" }
        ],
        Rows =
        [
            new DecisionTableRow
            {
                Id = "r1", Order = 1, Description = "Young applicant",
                InputCells  = [
                    new DecisionTableCell { ColumnId = "in-age",    Expression = "< 25" },
                    new DecisionTableCell { ColumnId = "in-income", Expression = "> 5000" },
                    new DecisionTableCell { ColumnId = "in-status", Expression = "\"active\"" }
                ],
                OutputCells = [
                    new DecisionTableCell { ColumnId = "out-risk",   Expression = "\"high\"" },
                    new DecisionTableCell { ColumnId = "out-factor", Expression = "1.5" }
                ]
            },
            new DecisionTableRow
            {
                Id = "r2", Order = 2,
                InputCells  = [
                    new DecisionTableCell { ColumnId = "in-age",    Expression = "[25..65]" },
                    new DecisionTableCell { ColumnId = "in-income", Expression = "> 3000" },
                    new DecisionTableCell { ColumnId = "in-status", Expression = "" }
                ],
                OutputCells = [
                    new DecisionTableCell { ColumnId = "out-risk",   Expression = "\"low\"" },
                    new DecisionTableCell { ColumnId = "out-factor", Expression = "0.8" }
                ]
            }
        ]
    };

    // ── Full round-trip test ────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_PreservesAllData()
    {
        DecisionTableModel original = BuildFullTable();
        DmnImportResult result = RoundTrip(original);

        Assert.True(result.IsSuccess);
        DecisionTableModel imported = result.Table!;

        // Column counts
        Assert.Equal(original.InputColumns.Count,  imported.InputColumns.Count);
        Assert.Equal(original.OutputColumns.Count, imported.OutputColumns.Count);
        Assert.Equal(original.Rows.Count,          imported.Rows.Count);

        // Input column names and data types
        for (int i = 0; i < original.InputColumns.Count; i++)
        {
            Assert.Equal(original.InputColumns[i].Name,     imported.InputColumns[i].Name);
            Assert.Equal(original.InputColumns[i].Label,    imported.InputColumns[i].Label);
            Assert.Equal(original.InputColumns[i].DataType, imported.InputColumns[i].DataType);
        }

        // Output column names and data types
        for (int i = 0; i < original.OutputColumns.Count; i++)
        {
            Assert.Equal(original.OutputColumns[i].Name,     imported.OutputColumns[i].Name);
            Assert.Equal(original.OutputColumns[i].Label,    imported.OutputColumns[i].Label);
            Assert.Equal(original.OutputColumns[i].DataType, imported.OutputColumns[i].DataType);
        }

        // Hit policy
        Assert.Equal(original.HitPolicy, imported.HitPolicy);

        // Row cell expressions
        for (int r = 0; r < original.Rows.Count; r++)
        {
            for (int c = 0; c < original.Rows[r].InputCells.Count; c++)
            {
                Assert.Equal(original.Rows[r].InputCells[c].Expression,
                             imported.Rows[r].InputCells[c].Expression);
            }
            for (int c = 0; c < original.Rows[r].OutputCells.Count; c++)
            {
                Assert.Equal(original.Rows[r].OutputCells[c].Expression,
                             imported.Rows[r].OutputCells[c].Expression);
            }
        }
    }

    // ── All hit policies round-trip ─────────────────────────────────────────

    [Theory]
    [InlineData(HitPolicy.First)]
    [InlineData(HitPolicy.Unique)]
    [InlineData(HitPolicy.Collect)]
    [InlineData(HitPolicy.Priority)]
    [InlineData(HitPolicy.OutputOrder)]
    [InlineData(HitPolicy.CollectSum)]
    [InlineData(HitPolicy.CollectMin)]
    [InlineData(HitPolicy.CollectMax)]
    [InlineData(HitPolicy.CollectCount)]
    public void RoundTrip_AllHitPolicies_PreservePolicy(HitPolicy hitPolicy)
    {
        DecisionTableModel original = BuildFullTable(hitPolicy);
        DmnImportResult result = RoundTrip(original);

        Assert.True(result.IsSuccess);
        Assert.Equal(hitPolicy, result.Table!.HitPolicy);
    }

    // ── FEEL expression round-trip ──────────────────────────────────────────

    [Theory]
    [InlineData("> 100")]
    [InlineData("[1..10]")]
    [InlineData("not(\"rejected\")")]
    [InlineData("< 25")]
    [InlineData("\"active\"")]
    [InlineData("true")]
    [InlineData("")]
    public void RoundTrip_FeelExpressions_PreservedVerbatim(string expression)
    {
        DecisionTableModel table = new()
        {
            Id = "feel-rt",
            Name = "FEEL Round Trip",
            HitPolicy = HitPolicy.First,
            InputColumns  = [new DecisionTableColumn { Id = "c1", Name = "val", DataType = "string" }],
            OutputColumns = [new DecisionTableColumn { Id = "c2", Name = "result", DataType = "string" }],
            Rows =
            [
                new DecisionTableRow
                {
                    Id = "r1", Order = 1,
                    InputCells  = [new DecisionTableCell { ColumnId = "c1", Expression = expression }],
                    OutputCells = [new DecisionTableCell { ColumnId = "c2", Expression = "\"ok\"" }]
                }
            ]
        };

        DmnImportResult result = RoundTrip(table);

        Assert.True(result.IsSuccess);
        Assert.Equal(expression, result.Table!.Rows[0].InputCells[0].Expression);
    }

    // ── Table name and id round-trip ────────────────────────────────────────

    [Fact]
    public void RoundTrip_TableNamePreserved()
    {
        DecisionTableModel original = new()
        {
            Id = "my-table",
            Name = "My Important Decision Table",
            HitPolicy = HitPolicy.Unique
        };

        DmnImportResult result = RoundTrip(original);

        Assert.True(result.IsSuccess);
        Assert.Equal("My Important Decision Table", result.Table!.Name);
    }

    // ── Empty table round-trip ───────────────────────────────────────────────

    [Fact]
    public void RoundTrip_EmptyTable_PreservesStructure()
    {
        DecisionTableModel original = new()
        {
            Id = "empty-rt",
            Name = "Empty",
            HitPolicy = HitPolicy.Unique
        };

        DmnImportResult result = RoundTrip(original);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Table!.InputColumns);
        Assert.Empty(result.Table!.OutputColumns);
        Assert.Empty(result.Table!.Rows);
    }
}
