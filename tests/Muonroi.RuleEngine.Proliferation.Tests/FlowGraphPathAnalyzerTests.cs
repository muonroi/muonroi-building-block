using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class FlowGraphPathAnalyzerTests
{
    /// <summary>
    /// Linear graph: Start -> NodeA -> End
    /// Expected: 1 path.
    /// </summary>
    [Fact]
    public void Analyze_LinearGraph_ReturnsOnePath()
    {
        const string json = """
        {
          "flowGraph": {
            "nodes": [
              { "id": "start", "type": "start", "label": "Start" },
              { "id": "nodeA", "type": "ruleTask", "label": "Node A" },
              { "id": "end", "type": "end", "label": "End" }
            ],
            "edges": [
              { "id": "e1", "source": "start", "target": "nodeA", "edgeType": "always" },
              { "id": "e2", "source": "nodeA", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """;

        FlowGraphAnalysis analysis = FlowGraphPathAnalyzer.Analyze(json);

        analysis.Paths.Should().HaveCount(1);
        analysis.Paths[0].NodeIds.Should().ContainInOrder("start", "nodeA", "end");
        analysis.Paths[0].EdgeTypes.Should().ContainInOrder("always", "always");
        analysis.Paths[0].Description.Should().Contain("Start");
        analysis.Paths[0].Description.Should().Contain("Node A");
    }

    /// <summary>
    /// Branching graph: Start -> Gateway -> [TrueNode, FalseNode] -> End
    /// Expected: 2 paths (one via on-true, one via on-false).
    /// </summary>
    [Fact]
    public void Analyze_BranchingGraph_ReturnsMultiplePaths()
    {
        const string json = """
        {
          "flowGraph": {
            "nodes": [
              { "id": "start", "type": "start", "label": "Start" },
              { "id": "gw", "type": "exclusiveGateway", "label": "Gateway" },
              { "id": "trueNode", "type": "ruleTask", "label": "True Path" },
              { "id": "falseNode", "type": "ruleTask", "label": "False Path" },
              { "id": "end", "type": "end", "label": "End" }
            ],
            "edges": [
              { "id": "e1", "source": "start", "target": "gw", "edgeType": "always" },
              { "id": "e2", "source": "gw", "target": "trueNode", "edgeType": "on-true" },
              { "id": "e3", "source": "gw", "target": "falseNode", "edgeType": "on-false" },
              { "id": "e4", "source": "trueNode", "target": "end", "edgeType": "always" },
              { "id": "e5", "source": "falseNode", "target": "end", "edgeType": "always" }
            ]
          }
        }
        """;

        FlowGraphAnalysis analysis = FlowGraphPathAnalyzer.Analyze(json);

        analysis.Paths.Should().HaveCountGreaterThanOrEqualTo(2);

        // One path should include "on-true" edge type
        bool hasOnTruePath = analysis.Paths.Any(p => p.EdgeTypes.Contains("on-true"));
        bool hasOnFalsePath = analysis.Paths.Any(p => p.EdgeTypes.Contains("on-false"));

        hasOnTruePath.Should().BeTrue("should have a path via on-true edge");
        hasOnFalsePath.Should().BeTrue("should have a path via on-false edge");
    }

    [Fact]
    public void Analyze_EmptyJson_ReturnsEmptyAnalysis()
    {
        FlowGraphAnalysis analysis = FlowGraphPathAnalyzer.Analyze("{}");

        analysis.Paths.Should().BeEmpty();
        analysis.Nodes.Should().BeEmpty();
        analysis.Edges.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_NodeWithOutputFields_ExtractsNodeSummary()
    {
        const string json = """
        {
          "flowGraph": {
            "nodes": [
              {
                "id": "node1",
                "type": "ruleTask",
                "label": "Validate Order",
                "data": {
                  "outputFields": [
                    { "path": "order.isValid", "dataType": "boolean" },
                    { "path": "order.errorCode", "dataType": "string" }
                  ]
                }
              }
            ],
            "edges": []
          }
        }
        """;

        FlowGraphAnalysis analysis = FlowGraphPathAnalyzer.Analyze(json);

        analysis.Nodes.Should().HaveCount(1);
        analysis.Nodes[0].Label.Should().Be("Validate Order");
        analysis.Nodes[0].OutputFields.Should().Contain("order.isValid");
        analysis.Nodes[0].OutputFields.Should().Contain("order.errorCode");
    }
}
