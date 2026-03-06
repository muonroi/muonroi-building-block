using Muonroi.RuleEngine.DecisionTable.Converters;
using Muonroi.RuleEngine.DecisionTable.Models;
using Muonroi.RuleEngine.DecisionTable.Serializers;
using Muonroi.RuleEngine.DecisionTable.Validators;
using Muonroi.RuleEngine.Abstractions;
using System.Text.Json;
using DecisionTableModel = Muonroi.RuleEngine.DecisionTable.Models.DecisionTable;

namespace Muonroi.RuleEngine.DecisionTable.Tests;

public sealed class DecisionTableConverterTests
{
    [Fact]
    public void Convert_SimpleTable_GeneratesWorkflowJson()
    {
        DecisionTableModel table = CreateAgeDecisionTable();
        DecisionTableToJsonConverter converter = new();

        string json = converter.Convert(table);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement workflow = doc.RootElement[0];
        Assert.Equal("AgeCheck", workflow.GetProperty("WorkflowName").GetString());
        Assert.Equal(2, workflow.GetProperty("Rules").GetArrayLength());
    }

    [Fact]
    public void Validate_UniqueWithOverlap_ReturnsError()
    {
        DecisionTableModel table = new()
        {
            Name = "OverlapCheck",
            HitPolicy = HitPolicy.Unique
        };
        DecisionTableColumn column = new()
        {
            Name = "x",
            Label = "X",
            DataType = "number"
        };
        table.InputColumns = [column];
        DecisionTableColumn tableColumn = new()
        {
            Name = "result",
            Label = "Result",
            DataType = "string"
        };
        table.OutputColumns = [tableColumn];
        DecisionTableRow row = new()
        {
            Order = 1,
            InputCells = [new DecisionTableCell { Expression = "> 10" }],
            OutputCells = [new DecisionTableCell { Expression = "A" }]
        };
        table.Rows = [
            row,
            new DecisionTableRow
            {
                Order = 2,
                InputCells = [new DecisionTableCell { Expression = "> 5" }],
                OutputCells = [new DecisionTableCell { Expression = "B" }]
            }
        ];

        DecisionTableValidator validator = new();
        ValidationResult result = validator.Validate(table);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Overlap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Serializer_RoundTrip_PreservesRows()
    {
        DecisionTableModel table = CreateAgeDecisionTable();
        DecisionTableJsonSerializer serializer = new();

        string json = DecisionTableJsonSerializer.Serialize(table);
        DecisionTableModel restored = DecisionTableJsonSerializer.Deserialize(json);

        Assert.Equal(table.Name, restored.Name);
        Assert.Equal(table.Rows.Count, restored.Rows.Count);
        Assert.Equal(table.InputColumns.Count, restored.InputColumns.Count);
    }

    [Fact]
    public async Task ToRuleConverter_CreatesExecutableRules()
    {
        DecisionTableModel table = CreateAgeDecisionTable();
        DecisionTableToRuleConverter converter = new();
        IRule<AgeContext>[] rules = [.. DecisionTableToRuleConverter.Convert<AgeContext>(table).OrderBy(x => x.Order)];
        Assert.Equal(2, rules.Length);

        AgeContext context = new()
        {
            Age = 20
        };
        RuleResult eval = await rules[0].EvaluateAsync(context, new FactBag(), CancellationToken.None);
        Assert.True(eval.IsSuccess);
    }

    private static DecisionTableModel CreateAgeDecisionTable()
    {
        DecisionTableColumn inputColumn = new()
        {
            Name = "Age",
            Label = "Age",
            DataType = "number"
        };
        DecisionTableColumn outputColumn = new()
        {
            Name = "CanDrive",
            Label = "CanDrive",
            DataType = "boolean"
        };
        DecisionTableModel table = new()
        {
            Name = "AgeCheck",
            HitPolicy = HitPolicy.First,
            InputColumns = [inputColumn],
            OutputColumns = [outputColumn],
            Rows = [
                new DecisionTableRow
                {
                    Order = 1,
                    InputCells = [new DecisionTableCell { ColumnId = inputColumn.Id, Expression = ">= 18" }],
                    OutputCells = [new DecisionTableCell { ColumnId = outputColumn.Id, Expression = "true" }]
                },
                new DecisionTableRow
                {
                    Order = 2,
                    InputCells = [new DecisionTableCell { ColumnId = inputColumn.Id, Expression = "< 18" }],
                    OutputCells = [new DecisionTableCell { ColumnId = outputColumn.Id, Expression = "false" }]
                }
            ]
        };
        return table;
    }

    private sealed class AgeContext
    {
        public int Age { get; set; }
    }
}
