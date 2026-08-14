using DecisionTableModel = Muonroi.RuleEngine.DecisionTable.Models.DecisionTable;

namespace Muonroi.RuleEngine.DecisionTable.Tests.Dmn;

/// <summary>
/// Tests for DmnExporter — verifies that Muonroi decision table models export
/// to valid DMN 1.3 XML with correct element structure, namespaces, and FEEL expressions.
/// </summary>
public sealed class DmnExporterTests
{
    private static readonly XNamespace DmnNs = DmnConstants.DmnNamespace;

    // ── Helper factory methods ──────────────────────────────────────────────

    private static DecisionTableModel EmptyTable() => new()
    {
        Id = "table-001",
        Name = "Empty Table",
        Description = "A table with no columns or rows",
        HitPolicy = HitPolicy.First
    };

    private static DecisionTableModel TableWithColumns() => new()
    {
        Id = "table-002",
        Name = "Risk Assessment",
        HitPolicy = HitPolicy.Unique,
        InputColumns =
        [
            new DecisionTableColumn { Id = "in-age",    Name = "applicantAge",   Label = "Age",    DataType = "number" },
            new DecisionTableColumn { Id = "in-income", Name = "monthlyIncome",  Label = "Income", DataType = "number" }
        ],
        OutputColumns =
        [
            new DecisionTableColumn { Id = "out-risk", Name = "riskCategory", Label = "Risk Category", DataType = "string" }
        ]
    };

    private static DecisionTableModel TableWithRows()
    {
        DecisionTableModel table = TableWithColumns();
        table.Rows =
        [
            new DecisionTableRow
            {
                Id = "row-1", Order = 1,
                InputCells  = [new DecisionTableCell { ColumnId = "in-age",    Expression = "< 25" },
                               new DecisionTableCell { ColumnId = "in-income", Expression = "> 5000" }],
                OutputCells = [new DecisionTableCell { ColumnId = "out-risk",  Expression = "\"high\"" }]
            },
            new DecisionTableRow
            {
                Id = "row-2", Order = 2,
                InputCells  = [new DecisionTableCell { ColumnId = "in-age",    Expression = "[25..65]" },
                               new DecisionTableCell { ColumnId = "in-income", Expression = "> 3000" }],
                OutputCells = [new DecisionTableCell { ColumnId = "out-risk",  Expression = "\"low\"" }]
            },
            new DecisionTableRow
            {
                Id = "row-3", Order = 3,
                InputCells  = [new DecisionTableCell { ColumnId = "in-age",    Expression = "> 65" },
                               new DecisionTableCell { ColumnId = "in-income", Expression = "" }],
                OutputCells = [new DecisionTableCell { ColumnId = "out-risk",  Expression = "\"medium\"" }]
            }
        ];
        return table;
    }

    // ── Namespace and root element tests ────────────────────────────────────

    [Fact]
    public void ExportToDmnXml_EmptyTable_ProducesValidXmlWithDmnNamespace()
    {
        string xml = DmnExporter.ExportToDmnXml(EmptyTable());
        XDocument doc = XDocument.Parse(xml);

        XElement? defs = doc.Root;
        Assert.NotNull(defs);
        Assert.Equal(DmnConstants.DmnNamespace.NamespaceName, defs.Name.NamespaceName);
        Assert.Equal("definitions", defs.Name.LocalName);
    }

    [Fact]
    public void ExportToDmnXml_EmptyTable_DefinitionsHasCorrectAttributes()
    {
        string xml = DmnExporter.ExportToDmnXml(EmptyTable());
        XDocument doc = XDocument.Parse(xml);
        XElement defs = doc.Root!;

        Assert.Equal("def_table-001", defs.Attribute("id")?.Value);
        Assert.Equal("Empty Table", defs.Attribute("name")?.Value);
        Assert.NotNull(defs.Attribute("namespace"));
    }

    [Fact]
    public void ExportToDmnXml_EmptyTable_ContainsDecisionAndDecisionTableElements()
    {
        string xml = DmnExporter.ExportToDmnXml(EmptyTable());
        XDocument doc = XDocument.Parse(xml);
        XElement defs = doc.Root!;

        XElement? decision = defs.Element(DmnNs + "decision");
        Assert.NotNull(decision);
        Assert.Equal("table-001", decision.Attribute("id")?.Value);

        XElement? decisionTable = decision.Element(DmnNs + "decisionTable");
        Assert.NotNull(decisionTable);
    }

    // ── Column structure tests ───────────────────────────────────────────────

    [Fact]
    public void ExportToDmnXml_TwoInputColumns_ProducesTwoInputElementsNotWrapped()
    {
        string xml = DmnExporter.ExportToDmnXml(TableWithColumns());
        XDocument doc = XDocument.Parse(xml);

        XElement decisionTable = doc.Root!
            .Element(DmnNs + "decision")!
            .Element(DmnNs + "decisionTable")!;

        List<XElement> inputs = [.. decisionTable.Elements(DmnNs + "input")];
        Assert.Equal(2, inputs.Count);

        // Each <input> must have a child <inputExpression> with a <text> grandchild
        foreach (XElement input in inputs)
        {
            XElement? ie = input.Element(DmnNs + "inputExpression");
            Assert.NotNull(ie);
            Assert.NotNull(ie.Element(DmnNs + "text"));
        }
    }

    [Fact]
    public void ExportToDmnXml_OneOutputColumn_ProducesOneOutputElement()
    {
        string xml = DmnExporter.ExportToDmnXml(TableWithColumns());
        XDocument doc = XDocument.Parse(xml);

        XElement decisionTable = doc.Root!
            .Element(DmnNs + "decision")!
            .Element(DmnNs + "decisionTable")!;

        List<XElement> outputs = [.. decisionTable.Elements(DmnNs + "output")];
        Assert.Single(outputs);
        Assert.Equal("riskCategory", outputs[0].Attribute("name")?.Value);
        Assert.Equal("Risk Category", outputs[0].Attribute("label")?.Value);
    }

    [Fact]
    public void ExportToDmnXml_InputColumn_HasCorrectAttributesAndTypeRef()
    {
        string xml = DmnExporter.ExportToDmnXml(TableWithColumns());
        XDocument doc = XDocument.Parse(xml);

        XElement decisionTable = doc.Root!
            .Element(DmnNs + "decision")!
            .Element(DmnNs + "decisionTable")!;

        XElement firstInput = decisionTable.Elements(DmnNs + "input").First();
        Assert.Equal("input_in-age", firstInput.Attribute("id")?.Value);
        Assert.Equal("Age", firstInput.Attribute("label")?.Value);

        XElement ie = firstInput.Element(DmnNs + "inputExpression")!;
        Assert.Equal("number", ie.Attribute("typeRef")?.Value);
        Assert.Equal("applicantAge", ie.Element(DmnNs + "text")?.Value);
    }

    // ── Row and cell structure tests ────────────────────────────────────────

    [Fact]
    public void ExportToDmnXml_ThreeRows_ProducesThreeRuleElements()
    {
        string xml = DmnExporter.ExportToDmnXml(TableWithRows());
        XDocument doc = XDocument.Parse(xml);

        XElement decisionTable = doc.Root!
            .Element(DmnNs + "decision")!
            .Element(DmnNs + "decisionTable")!;

        List<XElement> rules = [.. decisionTable.Elements(DmnNs + "rule")];
        Assert.Equal(3, rules.Count);
    }

    [Fact]
    public void ExportToDmnXml_Rule_HasInputEntryAndOutputEntryWithTextChildren()
    {
        string xml = DmnExporter.ExportToDmnXml(TableWithRows());
        XDocument doc = XDocument.Parse(xml);

        XElement firstRule = doc.Root!
            .Element(DmnNs + "decision")!
            .Element(DmnNs + "decisionTable")!
            .Elements(DmnNs + "rule")
            .First();

        List<XElement> inputEntries  = [.. firstRule.Elements(DmnNs + "inputEntry")];
        List<XElement> outputEntries = [.. firstRule.Elements(DmnNs + "outputEntry")];

        Assert.Equal(2, inputEntries.Count);
        Assert.Single(outputEntries);

        Assert.Equal("< 25", inputEntries[0].Element(DmnNs + "text")?.Value);
        Assert.Equal("> 5000", inputEntries[1].Element(DmnNs + "text")?.Value);
        Assert.Equal("\"high\"", outputEntries[0].Element(DmnNs + "text")?.Value);
    }

    // ── FEEL expression pass-through ───────────────────────────────────────

    [Fact]
    public void ExportToDmnXml_FeelExpressions_ArePreservedVerbatim()
    {
        DecisionTableModel table = new()
        {
            Id = "feel-test",
            Name = "FEEL Test",
            HitPolicy = HitPolicy.First,
            InputColumns  = [new DecisionTableColumn { Id = "c1", Name = "val", DataType = "number" }],
            OutputColumns = [new DecisionTableColumn { Id = "c2", Name = "result", DataType = "string" }],
            Rows =
            [
                new DecisionTableRow
                {
                    Id = "r1", Order = 1,
                    InputCells  = [new DecisionTableCell { ColumnId = "c1", Expression = "not(\"rejected\")" }],
                    OutputCells = [new DecisionTableCell { ColumnId = "c2", Expression = "[1..10]" }]
                }
            ]
        };

        string xml = DmnExporter.ExportToDmnXml(table);
        XDocument doc = XDocument.Parse(xml);
        XElement rule = doc.Root!.Element(DmnNs + "decision")!
            .Element(DmnNs + "decisionTable")!.Elements(DmnNs + "rule").First();

        Assert.Equal("not(\"rejected\")", rule.Elements(DmnNs + "inputEntry").First().Element(DmnNs + "text")?.Value);
        Assert.Equal("[1..10]", rule.Elements(DmnNs + "outputEntry").First().Element(DmnNs + "text")?.Value);
    }

    // ── Hit policy mapping tests ────────────────────────────────────────────

    [Theory]
    [InlineData(HitPolicy.First,       "FIRST",   null)]
    [InlineData(HitPolicy.Unique,      "UNIQUE",  null)]
    [InlineData(HitPolicy.Collect,     "COLLECT", null)]
    [InlineData(HitPolicy.Priority,    "PRIORITY", null)]
    [InlineData(HitPolicy.OutputOrder, "OUTPUT ORDER", null)]
    [InlineData(HitPolicy.CollectSum,  "COLLECT", "SUM")]
    [InlineData(HitPolicy.CollectMin,  "COLLECT", "MIN")]
    [InlineData(HitPolicy.CollectMax,  "COLLECT", "MAX")]
    [InlineData(HitPolicy.CollectCount,"COLLECT", "COUNT")]
    public void ExportToDmnXml_HitPolicy_MapsToCorrectDmnAttributes(
        HitPolicy policy, string expectedHitPolicy, string? expectedAggregation)
    {
        DecisionTableModel table = new()
        {
            Id = "hp-test",
            Name = "HP Test",
            HitPolicy = policy
        };

        string xml = DmnExporter.ExportToDmnXml(table);
        XDocument doc = XDocument.Parse(xml);
        XElement dt = doc.Root!.Element(DmnNs + "decision")!.Element(DmnNs + "decisionTable")!;

        Assert.Equal(expectedHitPolicy, dt.Attribute("hitPolicy")?.Value);
        Assert.Equal(expectedAggregation, dt.Attribute("aggregation")?.Value);
    }

    // ── DataType/typeRef mapping ────────────────────────────────────────────

    [Theory]
    [InlineData("string",  "string")]
    [InlineData("number",  "number")]
    [InlineData("boolean", "boolean")]
    [InlineData("date",    "date")]
    public void ExportToDmnXml_ColumnDataType_MapsToTypeRefAttribute(string dataType, string expectedTypeRef)
    {
        DecisionTableModel table = new()
        {
            Id = "type-test",
            Name = "Type Test",
            HitPolicy = HitPolicy.First,
            InputColumns = [new DecisionTableColumn { Id = "c1", Name = "val", DataType = dataType }]
        };

        string xml = DmnExporter.ExportToDmnXml(table);
        XDocument doc = XDocument.Parse(xml);
        XElement input = doc.Root!.Element(DmnNs + "decision")!
            .Element(DmnNs + "decisionTable")!.Elements(DmnNs + "input").First();
        XElement ie = input.Element(DmnNs + "inputExpression")!;

        Assert.Equal(expectedTypeRef, ie.Attribute("typeRef")?.Value);
    }

    // ── HitPolicyMapper round-trip ──────────────────────────────────────────

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
    public void HitPolicyMapper_RoundTrip_PreservesPolicy(HitPolicy policy)
    {
        (string hitPolicyStr, string? aggregation) = DmnHitPolicyMapper.ToDmnString(policy);
        HitPolicy result = DmnHitPolicyMapper.FromDmnString(hitPolicyStr, aggregation);
        Assert.Equal(policy, result);
    }

    [Fact]
    public void HitPolicyMapper_UnknownString_ReturnsUniqueDefault()
    {
        HitPolicy result = DmnHitPolicyMapper.FromDmnString("UNKNOWN_POLICY", null);
        Assert.Equal(HitPolicy.Unique, result);
    }
}
