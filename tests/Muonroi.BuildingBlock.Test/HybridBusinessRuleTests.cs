namespace Muonroi.BuildingBlock.Test;

public class HybridBusinessRuleTests
{
    private sealed class PositiveRule : IBusinessRule<int>
    {
        public string Code => "POS";

        public Task<bool> IsSatisfiedAsync(int context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(context > 0);
        }
    }

    [Fact]
    public async Task And_ComposesCodeAndExternalRules()
    {
        const string json = """
                            [
                              {
                                "WorkflowName": "NumberWorkflow",
                                "Rules": [
                                  {
                                    "RuleName": "IsEven",
                                    "RuleExpressionType": "LambdaExpression",
                                    "Expression": "input1.value % 2 == 0"
                                  }
                                ]
                              }
                            ]
                            """;
        IBusinessRule<int> external = new ExternalJsonRule<int>(json, "NumberWorkflow");
        IBusinessRule<int> combined = new PositiveRule().And(external);

        Assert.True(await combined.IsSatisfiedAsync(4));
        Assert.False(await combined.IsSatisfiedAsync(-2));
        Assert.False(await combined.IsSatisfiedAsync(3));
    }

    [Fact]
    public async Task Or_ComposesRules()
    {
        IBusinessRule<int> positive = new PositiveRule();

        const string json = """
                            [
                              {
                                "WorkflowName": "ZeroWorkflow",
                                "Rules": [
                                  {
                                    "RuleName": "NonZero",
                                    "RuleExpressionType": "LambdaExpression",
                                    "Expression": "input1.value != 0"
                                  }
                                ]
                              }
                            ]
                            """;
        IBusinessRule<int> nonZero = new ExternalJsonRule<int>(json, "ZeroWorkflow");

        IBusinessRule<int> combined = positive.Or(nonZero);

        Assert.True(await combined.IsSatisfiedAsync(5));
        Assert.True(await combined.IsSatisfiedAsync(-1));
        Assert.False(await combined.IsSatisfiedAsync(0));
    }
}
