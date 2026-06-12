using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class RuleSetSchemaExtractorTests
{
    [Fact]
    public void Extract_DecisionTable_ReturnsInputAndOutputColumns()
    {
        string json = """
        {
            "hitPolicy": "First",
            "inputColumns": [
                { "name": "amount", "dataType": "number", "isRequired": true },
                { "name": "currency", "dataType": "string", "isRequired": true },
                { "name": "region", "dataType": "string", "isRequired": false }
            ],
            "outputColumns": [
                { "name": "discount", "dataType": "number" }
            ]
        }
        """;

        RuleSetSchema schema = RuleSetSchemaExtractor.Extract(json, RuleSetKind.DecisionTable);

        schema.Kind.Should().Be(RuleSetKind.DecisionTable);
        schema.InputFields.Should().HaveCount(3);
        schema.InputFields[0].Name.Should().Be("amount");
        schema.InputFields[0].DataType.Should().Be("number");
        schema.InputFields[0].IsRequired.Should().BeTrue();
        schema.InputFields[2].IsRequired.Should().BeFalse();
        schema.OutputFields.Should().HaveCount(1);
        schema.OutputFields[0].Name.Should().Be("discount");
    }

    [Fact]
    public void Extract_FlowGraph_ExtractsFieldsFromFeelExpressions()
    {
        string json = """
        {
            "flowGraph": {
                "nodes": [
                    {
                        "id": "node1",
                        "data": {
                            "condition": "input.amount > 100 and input.currency = \"USD\"",
                            "outputFields": [
                                { "path": "result.approved", "dataType": "boolean" }
                            ]
                        }
                    }
                ],
                "edges": []
            }
        }
        """;

        RuleSetSchema schema = RuleSetSchemaExtractor.Extract(json, RuleSetKind.FlowGraph);

        schema.Kind.Should().Be(RuleSetKind.FlowGraph);
        schema.InputFields.Select(f => f.Name).Should().Contain("amount");
        schema.InputFields.Select(f => f.Name).Should().Contain("currency");
        schema.OutputFields.Should().HaveCount(1);
        schema.OutputFields[0].Name.Should().Be("result.approved");
    }

    [Fact]
    public void Extract_CodeBased_ExtractsFromParameters()
    {
        string json = """
        {
            "rules": [
                {
                    "ruleCode": "RULE_001",
                    "parameters": [
                        { "name": "customerId" },
                        { "name": "orderTotal" }
                    ]
                }
            ]
        }
        """;

        RuleSetSchema schema = RuleSetSchemaExtractor.Extract(json, RuleSetKind.CodeBased);

        schema.Kind.Should().Be(RuleSetKind.CodeBased);
        schema.InputFields.Select(f => f.Name).Should().Contain("customerId");
        schema.InputFields.Select(f => f.Name).Should().Contain("orderTotal");
    }

    [Fact]
    public void Extract_EmptyJson_ReturnsEmptySchema()
    {
        RuleSetSchema schema = RuleSetSchemaExtractor.Extract("{}", RuleSetKind.DecisionTable);

        schema.InputFields.Should().BeEmpty();
        schema.OutputFields.Should().BeEmpty();
    }

    [Fact]
    public void Extract_MalformedJson_ReturnsEmptySchema()
    {
        RuleSetSchema schema = RuleSetSchemaExtractor.Extract("not json", RuleSetKind.Unknown);

        schema.InputFields.Should().BeEmpty();
        schema.OutputFields.Should().BeEmpty();
    }

    [Fact]
    public void Extract_NullOrWhitespace_ReturnsEmptySchema()
    {
        RuleSetSchemaExtractor.Extract("", RuleSetKind.Unknown).InputFields.Should().BeEmpty();
        RuleSetSchemaExtractor.Extract(null!, RuleSetKind.Unknown).InputFields.Should().BeEmpty();
        RuleSetSchemaExtractor.Extract("   ", RuleSetKind.Unknown).InputFields.Should().BeEmpty();
    }

    [Fact]
    public void Extract_FlowGraph_ExtractsFromTriggerData()
    {
        string json = """
        {
            "nodes": [
                {
                    "id": "start",
                    "data": {
                        "triggerData": {
                            "inputSchema": {
                                "customerName": "string",
                                "orderAmount": "number"
                            }
                        }
                    }
                }
            ],
            "edges": []
        }
        """;

        RuleSetSchema schema = RuleSetSchemaExtractor.Extract(json, RuleSetKind.FlowGraph);

        schema.InputFields.Select(f => f.Name).Should().Contain("customerName");
        schema.InputFields.Select(f => f.Name).Should().Contain("orderAmount");
    }

    [Fact]
    public void Extract_Unknown_ReturnsEmptySchema()
    {
        RuleSetSchema schema = RuleSetSchemaExtractor.Extract("""{"foo": "bar"}""", RuleSetKind.Unknown);
        schema.InputFields.Should().BeEmpty();
    }
}
