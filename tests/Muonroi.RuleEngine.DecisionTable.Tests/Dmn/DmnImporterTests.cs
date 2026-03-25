using Muonroi.RuleEngine.DecisionTable.Dmn;
using Muonroi.RuleEngine.DecisionTable.Models;
using DecisionTableModel = Muonroi.RuleEngine.DecisionTable.Models.DecisionTable;

namespace Muonroi.RuleEngine.DecisionTable.Tests.Dmn;

/// <summary>
/// Tests for DmnImporter — verifies that valid DMN 1.3 XML is correctly parsed
/// into Muonroi DecisionTableModel with all columns, rows, and expressions preserved.
/// </summary>
public sealed class DmnImporterTests
{
    // ── Minimal DMN XML fixtures ─────────────────────────────────────────────

    private const string MinimalDmn = """
        <?xml version="1.0" encoding="utf-8"?>
        <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/"
                     id="def_t1" name="Minimal" namespace="https://muonroi.dev/dmn">
          <decision id="t1" name="Minimal Table">
            <decisionTable id="dt_t1" hitPolicy="UNIQUE">
              <input id="input_age" label="Age">
                <inputExpression id="ie_age" typeRef="number">
                  <text>applicantAge</text>
                </inputExpression>
              </input>
              <output id="output_risk" name="riskCategory" label="Risk" typeRef="string" />
              <rule id="row-1">
                <inputEntry id="ie_row-1_0"><text>&gt; 18</text></inputEntry>
                <outputEntry id="oe_row-1_0"><text>"eligible"</text></outputEntry>
              </rule>
            </decisionTable>
          </decision>
        </definitions>
        """;

    private const string CollectSumDmn = """
        <?xml version="1.0" encoding="utf-8"?>
        <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/"
                     id="def_t2" name="Collect Sum" namespace="https://muonroi.dev/dmn">
          <decision id="t2" name="Score Table">
            <decisionTable id="dt_t2" hitPolicy="COLLECT" aggregation="SUM">
              <input id="input_c1" label="Category">
                <inputExpression id="ie_c1" typeRef="string">
                  <text>category</text>
                </inputExpression>
              </input>
              <output id="output_score" name="score" label="Score" typeRef="number" />
              <rule id="row-1">
                <inputEntry id="ie_row-1_0"><text>"A"</text></inputEntry>
                <outputEntry id="oe_row-1_0"><text>10</text></outputEntry>
              </rule>
            </decisionTable>
          </decision>
        </definitions>
        """;

    private const string MultipleDecisionsDmn = """
        <?xml version="1.0" encoding="utf-8"?>
        <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/"
                     id="def_multi" name="Multi" namespace="https://muonroi.dev/dmn">
          <decision id="d1" name="First Decision">
            <decisionTable id="dt_d1" hitPolicy="FIRST">
              <input id="in1" label="In1">
                <inputExpression id="ie1" typeRef="string"><text>val</text></inputExpression>
              </input>
              <output id="out1" name="result" label="Result" typeRef="string" />
            </decisionTable>
          </decision>
          <decision id="d2" name="Second Decision">
            <decisionTable id="dt_d2" hitPolicy="UNIQUE">
              <input id="in2" label="In2">
                <inputExpression id="ie2" typeRef="string"><text>val2</text></inputExpression>
              </input>
              <output id="out2" name="result2" label="Result2" typeRef="string" />
            </decisionTable>
          </decision>
        </definitions>
        """;

    // ── Basic import tests ───────────────────────────────────────────────────

    [Fact]
    public void ImportFromDmnXml_MinimalDmn_ReturnsSuccess()
    {
        DmnImportResult result = DmnImporter.ImportFromDmnXml(MinimalDmn);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Table);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ImportFromDmnXml_MinimalDmn_CorrectColumnAndRowCounts()
    {
        DmnImportResult result = DmnImporter.ImportFromDmnXml(MinimalDmn);

        DecisionTableModel table = result.Table!;
        Assert.Single(table.InputColumns);
        Assert.Single(table.OutputColumns);
        Assert.Single(table.Rows);
    }

    [Fact]
    public void ImportFromDmnXml_MinimalDmn_InputColumnHasCorrectProperties()
    {
        DmnImportResult result = DmnImporter.ImportFromDmnXml(MinimalDmn);
        DecisionTableColumn inputCol = result.Table!.InputColumns[0];

        Assert.Equal("applicantAge", inputCol.Name);
        Assert.Equal("Age", inputCol.Label);
        Assert.Equal("number", inputCol.DataType);
    }

    [Fact]
    public void ImportFromDmnXml_MinimalDmn_OutputColumnHasCorrectProperties()
    {
        DmnImportResult result = DmnImporter.ImportFromDmnXml(MinimalDmn);
        DecisionTableColumn outputCol = result.Table!.OutputColumns[0];

        Assert.Equal("riskCategory", outputCol.Name);
        Assert.Equal("Risk", outputCol.Label);
        Assert.Equal("string", outputCol.DataType);
    }

    [Fact]
    public void ImportFromDmnXml_MinimalDmn_UniqueHitPolicyMapped()
    {
        DmnImportResult result = DmnImporter.ImportFromDmnXml(MinimalDmn);
        Assert.Equal(HitPolicy.Unique, result.Table!.HitPolicy);
    }

    [Fact]
    public void ImportFromDmnXml_MinimalDmn_FeelExpressionsPreserved()
    {
        DmnImportResult result = DmnImporter.ImportFromDmnXml(MinimalDmn);
        DecisionTableRow row = result.Table!.Rows[0];

        Assert.Equal("> 18", row.InputCells[0].Expression);
        Assert.Equal("\"eligible\"", row.OutputCells[0].Expression);
    }

    // ── Hit policy mapping ───────────────────────────────────────────────────

    [Fact]
    public void ImportFromDmnXml_CollectWithSumAggregation_MapsToCollectSum()
    {
        DmnImportResult result = DmnImporter.ImportFromDmnXml(CollectSumDmn);

        Assert.True(result.IsSuccess);
        Assert.Equal(HitPolicy.CollectSum, result.Table!.HitPolicy);
    }

    // ── Multiple decisions ───────────────────────────────────────────────────

    [Fact]
    public void ImportFromDmnXml_MultipleDecisions_ImportsFirstWithWarning()
    {
        DmnImportResult result = DmnImporter.ImportFromDmnXml(MultipleDecisionsDmn);

        Assert.True(result.IsSuccess);
        Assert.Equal("d1", result.Table!.Id);
        Assert.NotEmpty(result.Warnings);
    }

    // ── Error handling ───────────────────────────────────────────────────────

    [Fact]
    public void ImportFromDmnXml_MalformedXml_ReturnsFailureWithErrors()
    {
        DmnImportResult result = DmnImporter.ImportFromDmnXml("<this is not valid xml <<<>>>");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Table);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ImportFromDmnXml_EmptyString_ReturnsFailure()
    {
        DmnImportResult result = DmnImporter.ImportFromDmnXml(string.Empty);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ImportFromDmnXml_XmlWithNoDecisionTable_ReturnsFailure()
    {
        const string noDecisionTable = """
            <?xml version="1.0" encoding="utf-8"?>
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/"
                         id="def_x" name="X" namespace="https://muonroi.dev/dmn">
              <decision id="d1" name="D1">
                <!-- No decisionTable child -->
              </decision>
            </definitions>
            """;

        DmnImportResult result = DmnImporter.ImportFromDmnXml(noDecisionTable);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ImportFromDmnXml_XmlWithNoDecision_ReturnsFailure()
    {
        const string noDecision = """
            <?xml version="1.0" encoding="utf-8"?>
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/"
                         id="def_x" name="X" namespace="https://muonroi.dev/dmn">
            </definitions>
            """;

        DmnImportResult result = DmnImporter.ImportFromDmnXml(noDecision);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    // ── Stream overload ──────────────────────────────────────────────────────

    [Fact]
    public void ImportFromDmnXml_StreamOverload_WorksCorrectly()
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(MinimalDmn);
        using MemoryStream stream = new(bytes);

        DmnImportResult result = DmnImporter.ImportFromDmnXml(stream);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Table!.InputColumns);
    }

    // ── Optional attribute defaults ──────────────────────────────────────────

    [Fact]
    public void ImportFromDmnXml_MissingTypeRef_DefaultsToString()
    {
        const string noTypeRef = """
            <?xml version="1.0" encoding="utf-8"?>
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/"
                         id="def_t" name="T" namespace="https://muonroi.dev/dmn">
              <decision id="t" name="T">
                <decisionTable id="dt_t" hitPolicy="UNIQUE">
                  <input id="in1" label="Val">
                    <inputExpression id="ie1">
                      <text>val</text>
                    </inputExpression>
                  </input>
                  <output id="out1" name="result" label="Result" />
                </decisionTable>
              </decision>
            </definitions>
            """;

        DmnImportResult result = DmnImporter.ImportFromDmnXml(noTypeRef);

        Assert.True(result.IsSuccess);
        Assert.Equal("string", result.Table!.InputColumns[0].DataType);
        Assert.Equal("string", result.Table!.OutputColumns[0].DataType);
    }
}
