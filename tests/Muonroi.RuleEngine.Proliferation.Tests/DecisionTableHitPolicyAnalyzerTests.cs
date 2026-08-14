namespace Muonroi.RuleEngine.Proliferation.Tests;

public class DecisionTableHitPolicyAnalyzerTests
{
    // ──────────────────────────────────────────────
    // Null / empty inputs
    // ──────────────────────────────────────────────

    [Fact]
    public void Analyze_NullJson_ReturnsEmptyAnalysis()
    {
        HitPolicyAnalysis result = DecisionTableHitPolicyAnalyzer.Analyze(null!);

        result.HitPolicy.Should().Be("First");
        result.RowCount.Should().Be(0);
        result.InputColumns.Should().BeEmpty();
        result.PolicyHints.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_EmptyJson_ReturnsEmptyAnalysis()
    {
        HitPolicyAnalysis result = DecisionTableHitPolicyAnalyzer.Analyze(string.Empty);

        result.HitPolicy.Should().Be("First");
        result.RowCount.Should().Be(0);
        result.InputColumns.Should().BeEmpty();
        result.PolicyHints.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────
    // Hit policy hints
    // ──────────────────────────────────────────────

    [Fact]
    public void Analyze_FirstPolicy_ProducesFirstMatchHint()
    {
        string json = """
            {
              "hitPolicy": "First",
              "inputColumns": [
                { "name": "amount", "dataType": "number" },
                { "name": "category", "dataType": "string" }
              ],
              "rules": [
                { "conditions": ["amount > 100", "category = 'A'"] },
                { "conditions": ["amount > 50", "category = 'B'"] },
                { "conditions": ["amount > 10", "category = 'C'"] }
              ]
            }
            """;

        HitPolicyAnalysis result = DecisionTableHitPolicyAnalyzer.Analyze(json);

        result.HitPolicy.Should().Be("First");
        result.RowCount.Should().Be(3);
        result.InputColumns.Should().Contain("amount");
        result.InputColumns.Should().Contain("category");
        result.PolicyHints.Should().HaveCountGreaterThanOrEqualTo(1);
        result.PolicyHints.Should().Contain(h => h.Contains("first") || h.Contains("order") || h.Contains("match"));
    }

    [Fact]
    public void Analyze_UniquePolicy_ProducesUniquenessHint()
    {
        string json = """
            {
              "hitPolicy": "Unique",
              "inputColumns": [
                { "name": "score", "dataType": "number" }
              ],
              "rules": [
                { "conditions": ["score > 90"] },
                { "conditions": ["score > 70"] }
              ]
            }
            """;

        HitPolicyAnalysis result = DecisionTableHitPolicyAnalyzer.Analyze(json);

        result.HitPolicy.Should().Be("Unique");
        result.RowCount.Should().Be(2);
        result.PolicyHints.Should().HaveCountGreaterThanOrEqualTo(1);
        result.PolicyHints.Should().Contain(h => h.Contains("unique") || h.Contains("overlap") || h.Contains("violation"));
    }

    [Fact]
    public void Analyze_CollectPolicy_ProducesAggregationHint()
    {
        string json = """
            {
              "hitPolicy": "Collect",
              "inputColumns": [
                { "name": "type", "dataType": "string" }
              ],
              "rules": [
                { "conditions": ["type = 'A'"] },
                { "conditions": ["type = 'B'"] },
                { "conditions": ["type = 'C'"] },
                { "conditions": ["type = 'D'"] }
              ]
            }
            """;

        HitPolicyAnalysis result = DecisionTableHitPolicyAnalyzer.Analyze(json);

        result.HitPolicy.Should().Be("Collect");
        result.RowCount.Should().Be(4);
        result.PolicyHints.Should().HaveCountGreaterThanOrEqualTo(1);
        result.PolicyHints.Should().Contain(h => h.Contains("collect") || h.Contains("aggregat") || h.Contains("multiple"));
    }

    [Fact]
    public void Analyze_PriorityPolicy_ProducesPriorityOrderHint()
    {
        string json = """
            {
              "hitPolicy": "Priority",
              "inputColumns": [
                { "name": "level", "dataType": "string" }
              ],
              "rules": [
                { "conditions": ["level = 'VIP'"] },
                { "conditions": ["level = 'Gold'"] }
              ]
            }
            """;

        HitPolicyAnalysis result = DecisionTableHitPolicyAnalyzer.Analyze(json);

        result.HitPolicy.Should().Be("Priority");
        result.RowCount.Should().Be(2);
        result.PolicyHints.Should().HaveCountGreaterThanOrEqualTo(1);
        result.PolicyHints.Should().Contain(h => h.Contains("priority") || h.Contains("order"));
    }

    // ──────────────────────────────────────────────
    // Default policy when hitPolicy is missing
    // ──────────────────────────────────────────────

    [Fact]
    public void Analyze_MissingHitPolicy_DefaultsToFirst()
    {
        string json = """
            {
              "inputColumns": [
                { "name": "value", "dataType": "number" }
              ],
              "rules": [
                { "conditions": ["value > 0"] }
              ]
            }
            """;

        HitPolicyAnalysis result = DecisionTableHitPolicyAnalyzer.Analyze(json);

        result.HitPolicy.Should().Be("First");
        result.RowCount.Should().Be(1);
        result.PolicyHints.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    // ──────────────────────────────────────────────
    // Row and column extraction
    // ──────────────────────────────────────────────

    [Fact]
    public void Analyze_WithMultipleColumns_ExtractsAllColumnNames()
    {
        string json = """
            {
              "hitPolicy": "First",
              "inputColumns": [
                { "name": "age", "dataType": "number" },
                { "name": "income", "dataType": "number" },
                { "name": "region", "dataType": "string" }
              ],
              "rules": []
            }
            """;

        HitPolicyAnalysis result = DecisionTableHitPolicyAnalyzer.Analyze(json);

        result.InputColumns.Should().HaveCount(3);
        result.InputColumns.Should().ContainInOrder("age", "income", "region");
        result.RowCount.Should().Be(0);
    }
}
