namespace Muonroi.Rules.Tests.Flags;

public class FeatureFlagEvaluatorTests
{
    [Fact]
    public void Evaluate_WhenFlagEnabled_ReturnsNewRulesResult()
    {
        var client = Substitute.For<IFeatureFlagClient>();
        client.IsEnabled("test-flag", Arg.Any<FeatureContext>()).Returns(true);
        var logger = Substitute.For<IMLog<FeatureFlagEvaluator>>();
        var evaluator = new FeatureFlagEvaluator(client, logger);

        string result = evaluator.Evaluate("test-flag", new FeatureContext("t1"), () => "old", () => "new");

        result.Should().Be("new");
    }

    [Fact]
    public void Evaluate_WhenFlagDisabled_ReturnsCurrentResult()
    {
        var client = Substitute.For<IFeatureFlagClient>();
        client.IsEnabled("test-flag", Arg.Any<FeatureContext>()).Returns(false);
        var logger = Substitute.For<IMLog<FeatureFlagEvaluator>>();
        var evaluator = new FeatureFlagEvaluator(client, logger);

        string result = evaluator.Evaluate("test-flag", new FeatureContext("t1"), () => "old", () => "new");

        result.Should().Be("old");
    }

    [Fact]
    public void FeatureContext_DefaultSegment_IsNull()
    {
        var ctx = new FeatureContext("tenant1");
        ctx.Segment.Should().BeNull();
        ctx.TenantId.Should().Be("tenant1");
    }
}
