namespace Muonroi.RuleEngine.Proliferation.Tests;

public class CrossRuleDependencyAnalyzerTests
{
    /// <summary>
    /// Node A outputs "user.verified", Node B reads "input.user.verified".
    /// Expected: 1 dependency detected.
    /// </summary>
    [Fact]
    public void Analyze_ProducerConsumerDependency_DetectsFactKeyDependency()
    {
        const string json = """
        {
          "flowGraph": {
            "nodes": [
              {
                "id": "nodeA",
                "type": "ruleTask",
                "label": "Verify User",
                "data": {
                  "outputFields": [
                    { "path": "user.verified", "dataType": "boolean" }
                  ]
                }
              },
              {
                "id": "nodeB",
                "type": "ruleTask",
                "label": "Apply Discount",
                "data": {
                  "expression": { "body": "if input.user.verified then 0.1 else 0" }
                }
              }
            ],
            "edges": [
              { "id": "e1", "source": "nodeA", "target": "nodeB", "edgeType": "always" }
            ]
          }
        }
        """;

        CrossRuleAnalysis analysis = CrossRuleDependencyAnalyzer.Analyze(json);

        analysis.Dependencies.Should().HaveCountGreaterThanOrEqualTo(1);

        FactKeyDependency dep = analysis.Dependencies.First(d => d.FactKey == "user.verified");
        dep.ProducerNodeId.Should().Be("nodeA");
        dep.ProducerLabel.Should().Be("Verify User");
        dep.ConsumerNodeId.Should().Be("nodeB");
        dep.ConsumerLabel.Should().Be("Apply Discount");
        dep.ProducerDataType.Should().Be("boolean");
    }

    /// <summary>
    /// Two nodes both output "order.total" — this is a duplicate-producer conflict.
    /// </summary>
    [Fact]
    public void Analyze_DuplicateProducers_DetectsConflict()
    {
        const string json = """
        {
          "flowGraph": {
            "nodes": [
              {
                "id": "nodeA",
                "type": "ruleTask",
                "label": "Calculate Total A",
                "data": {
                  "outputFields": [
                    { "path": "order.total", "dataType": "number" }
                  ]
                }
              },
              {
                "id": "nodeB",
                "type": "ruleTask",
                "label": "Calculate Total B",
                "data": {
                  "outputFields": [
                    { "path": "order.total", "dataType": "number" }
                  ]
                }
              }
            ],
            "edges": [
              { "id": "e1", "source": "nodeA", "target": "nodeB", "edgeType": "always" }
            ]
          }
        }
        """;

        CrossRuleAnalysis analysis = CrossRuleDependencyAnalyzer.Analyze(json);

        analysis.Conflicts.Should().HaveCountGreaterThanOrEqualTo(1);

        FactKeyConflict conflict = analysis.Conflicts.First(c => c.FactKey == "order.total");
        conflict.ConflictType.Should().Be("duplicate-producer");
    }

    [Fact]
    public void Analyze_NoDependencies_ReturnsEmptyCollections()
    {
        const string json = """
        {
          "flowGraph": {
            "nodes": [
              {
                "id": "nodeA",
                "type": "ruleTask",
                "label": "Standalone Node",
                "data": {
                  "outputFields": [
                    { "path": "result.value", "dataType": "string" }
                  ]
                }
              }
            ],
            "edges": []
          }
        }
        """;

        CrossRuleAnalysis analysis = CrossRuleDependencyAnalyzer.Analyze(json);

        // No consumer nodes => no dependencies
        analysis.Dependencies.Should().BeEmpty();
        // Only 1 node producing => no conflicts
        analysis.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_InvalidJson_ReturnsEmptyAnalysis()
    {
        CrossRuleAnalysis analysis = CrossRuleDependencyAnalyzer.Analyze("not-valid-json{");

        analysis.Dependencies.Should().BeEmpty();
        analysis.Conflicts.Should().BeEmpty();
    }
}
