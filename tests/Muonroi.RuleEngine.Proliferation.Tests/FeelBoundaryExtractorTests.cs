namespace Muonroi.RuleEngine.Proliferation.Tests;

public class FeelBoundaryExtractorTests
{
    // ──────────────────────────────────────────────
    // Empty / null / non-FEEL inputs
    // ──────────────────────────────────────────────

    [Fact]
    public void Extract_NullExpression_ReturnsEmptyResult()
    {
        BoundaryExtractionResult result = FeelBoundaryExtractor.Extract(null!);

        result.Boundaries.Should().BeEmpty();
        result.Branches.Should().BeEmpty();
    }

    [Fact]
    public void Extract_EmptyExpression_ReturnsEmptyResult()
    {
        BoundaryExtractionResult result = FeelBoundaryExtractor.Extract(string.Empty);

        result.Boundaries.Should().BeEmpty();
        result.Branches.Should().BeEmpty();
    }

    [Fact]
    public void Extract_WhitespaceExpression_ReturnsEmptyResult()
    {
        BoundaryExtractionResult result = FeelBoundaryExtractor.Extract("   ");

        result.Boundaries.Should().BeEmpty();
        result.Branches.Should().BeEmpty();
    }

    [Fact]
    public void Extract_NonFeelExpression_ReturnsEmptyResult()
    {
        BoundaryExtractionResult result = FeelBoundaryExtractor.Extract("just some text without comparisons");

        result.Boundaries.Should().BeEmpty();
        result.Branches.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────
    // Single comparison operators
    // ──────────────────────────────────────────────

    [Fact]
    public void Extract_GreaterThan_ProducesNMinusOneNNPlusOne()
    {
        BoundaryExtractionResult result = FeelBoundaryExtractor.Extract("input.amount > 100");

        result.Boundaries.Should().HaveCount(1);
        FieldBoundary b = result.Boundaries[0];
        b.Field.Should().Be("amount");
        b.Operator.Should().Be(">");
        b.ThresholdValue.Should().Be(100.0);
        b.BoundaryValues.Should().ContainInOrder(new object[] { 99.0, 100.0, 101.0 });
    }

    [Fact]
    public void Extract_LessThan_ProducesNMinusOneNNPlusOne()
    {
        BoundaryExtractionResult result = FeelBoundaryExtractor.Extract("input.price < 50");

        result.Boundaries.Should().HaveCount(1);
        FieldBoundary b = result.Boundaries[0];
        b.Field.Should().Be("price");
        b.Operator.Should().Be("<");
        b.ThresholdValue.Should().Be(50.0);
        b.BoundaryValues.Should().ContainInOrder(new object[] { 49.0, 50.0, 51.0 });
    }

    [Fact]
    public void Extract_GreaterThanOrEqual_ProducesNMinusOneNNPlusOne()
    {
        BoundaryExtractionResult result = FeelBoundaryExtractor.Extract("input.age >= 18");

        result.Boundaries.Should().HaveCount(1);
        FieldBoundary b = result.Boundaries[0];
        b.Field.Should().Be("age");
        b.Operator.Should().Be(">=");
        b.ThresholdValue.Should().Be(18.0);
        b.BoundaryValues.Should().ContainInOrder(new object[] { 17.0, 18.0, 19.0 });
    }

    [Fact]
    public void Extract_LessThanOrEqual_ProducesNMinusOneNNPlusOne()
    {
        BoundaryExtractionResult result = FeelBoundaryExtractor.Extract("input.age <= 65");

        result.Boundaries.Should().HaveCount(1);
        FieldBoundary b = result.Boundaries[0];
        b.Field.Should().Be("age");
        b.Operator.Should().Be("<=");
        b.ThresholdValue.Should().Be(65.0);
        b.BoundaryValues.Should().ContainInOrder(new object[] { 64.0, 65.0, 66.0 });
    }

    [Fact]
    public void Extract_AndExpression_ProducesTwoBoundaries()
    {
        BoundaryExtractionResult result = FeelBoundaryExtractor.Extract("input.age >= 18 and input.age <= 65");

        result.Boundaries.Should().HaveCount(2);

        FieldBoundary b1 = result.Boundaries[0];
        b1.Field.Should().Be("age");
        b1.Operator.Should().Be(">=");
        b1.ThresholdValue.Should().Be(18.0);
        b1.BoundaryValues.Should().ContainInOrder(new object[] { 17.0, 18.0, 19.0 });

        FieldBoundary b2 = result.Boundaries[1];
        b2.Field.Should().Be("age");
        b2.Operator.Should().Be("<=");
        b2.ThresholdValue.Should().Be(65.0);
        b2.BoundaryValues.Should().ContainInOrder(new object[] { 64.0, 65.0, 66.0 });
    }

    // ──────────────────────────────────────────────
    // Decimal boundaries
    // ──────────────────────────────────────────────

    [Fact]
    public void Extract_DecimalThreshold_ProducesDecimalBoundaries()
    {
        BoundaryExtractionResult result = FeelBoundaryExtractor.Extract("input.rate > 0.5");

        result.Boundaries.Should().HaveCount(1);
        FieldBoundary b = result.Boundaries[0];
        b.ThresholdValue.Should().BeApproximately(0.5, 0.0001);
        b.BoundaryValues.Should().HaveCount(3);
        ((double)b.BoundaryValues[0]).Should().BeApproximately(0.49, 0.001);
        ((double)b.BoundaryValues[1]).Should().BeApproximately(0.5, 0.001);
        ((double)b.BoundaryValues[2]).Should().BeApproximately(0.51, 0.001);
    }

    // ──────────────────────────────────────────────
    // Between expression
    // ──────────────────────────────────────────────

    [Fact]
    public void Extract_Between_ProducesFiveBoundaryValues()
    {
        BoundaryExtractionResult result = FeelBoundaryExtractor.Extract("input.score between 50 and 80");

        result.Boundaries.Should().HaveCount(1);
        FieldBoundary b = result.Boundaries[0];
        b.Field.Should().Be("score");
        b.Operator.Should().Be("between");
        // Expected: X-1, X, midpoint, Y, Y+1 → 49, 50, 65, 80, 81
        b.BoundaryValues.Should().HaveCount(5);
        b.BoundaryValues.Should().ContainInOrder(new object[] { 49.0, 50.0, 65.0, 80.0, 81.0 });
    }

    // ──────────────────────────────────────────────
    // If-then-else branches
    // ──────────────────────────────────────────────

    [Fact]
    public void Extract_IfThenElse_ProducesBranchHints()
    {
        BoundaryExtractionResult result = FeelBoundaryExtractor.Extract("if input.vip = true then \"premium\" else \"standard\"");

        result.Branches.Should().HaveCount(1);
        BranchHint branch = result.Branches[0];
        branch.Field.Should().Be("vip");
        branch.TruePath.Should().NotBeNullOrEmpty();
        branch.FalsePath.Should().NotBeNullOrEmpty();
    }

    // ──────────────────────────────────────────────
    // Ruleset JSON extraction
    // ──────────────────────────────────────────────

    [Fact]
    public void Extract_FromRulesetJson_ExtractsFEELConditions()
    {
        string ruleSetJson = """
            {
              "nodes": [
                { "id": "rule1", "type": "RuleTask", "data": { "condition": "input.amount > 100" } },
                { "id": "rule2", "type": "RuleTask", "data": { "condition": "input.age >= 18 and input.age <= 65" } }
              ]
            }
            """;

        BoundaryExtractionResult result = FeelBoundaryExtractor.Extract(ruleSetJson);

        result.Boundaries.Should().HaveCountGreaterThanOrEqualTo(3); // amount, age>=18, age<=65
    }
}
