using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.RuleEngine.Runtime.Web.Services;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class MDefaultRuleFlowContractProviderTests
{
    [Fact]
    public async Task MGetContractAsync_ShouldMapRequestAndResponseSchemas()
    {
        MDefaultRuleFlowContractProvider provider = CreateProvider();

        var response = await provider.MGetContractAsync("rule", "FLOW_TEST_PRIMARY");

        response.Should().NotBeNull();
        response!.SourceType.Should().Be("rule");
        response.RequestContract.Should().NotBeNull();
        response.RequestContract!.Fields.Should().ContainSingle(x => x.Path == "request");
        response.RequestContract.Fields[0].Children.Should().ContainSingle(x => x.Path == "request.amount" && x.Required);
        response.ResponseContract.Should().BeNull();
    }

    [Fact]
    public async Task MGetFlowContractAsync_ShouldUseSharedContext_WhenFlowPrefixMatches()
    {
        MDefaultRuleFlowContractProvider provider = CreateProvider();

        var response = await provider.MGetFlowContractAsync("FLOW_TEST_RULES");

        response.Should().NotBeNull();
        response!.SourceType.Should().Be("flow");
        response.SourceCode.Should().Be("FLOW_TEST_RULES");
        response.RequestContract.Should().NotBeNull();
        response.RequestContract!.Fields.Should().ContainSingle(x => x.Path == "request");
        response.RequestContract.Fields[0].Children.Should().ContainSingle(x => x.Path == "request.amount");
    }

    [Fact]
    public async Task MGetFlowContractAsync_ShouldReturnEmptyContracts_WhenFlowIsUnknown()
    {
        MDefaultRuleFlowContractProvider provider = CreateProvider();

        var response = await provider.MGetFlowContractAsync("UNKNOWN_FLOW");

        response.Should().NotBeNull();
        response!.RequestContract.Should().NotBeNull();
        response.ResponseContract.Should().BeNull();
    }

    [Fact]
    public async Task MGetNodeAuthoringContractAsync_ShouldResolvePrefixedNodeId_AndIncludeConsumedFacts()
    {
        MDefaultRuleFlowContractProvider provider = CreateProvider();

        var response = await provider.MGetNodeAuthoringContractAsync("FLOW_TEST_RULES", "node-rule-primary");

        response.Should().NotBeNull();
        response!.RequestScope.Should().NotBeNull();
        response.ResponseDelta.Should().BeNull();
        response.AvailableInputKeys.Should().BeNull();
    }

    [Fact]
    public async Task MGetContractAsync_ShouldReturnNull_WhenRuleCodeIsUnknown()
    {
        MDefaultRuleFlowContractProvider provider = CreateProvider();

        var response = await provider.MGetContractAsync("rule", "UNKNOWN_RULE");

        response.Should().BeNull();
    }

    private static MDefaultRuleFlowContractProvider CreateProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton(new MRuleAuthoringManifestRegistry(assemblies: [typeof(FlowPrimaryRule).Assembly]));
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        return new MDefaultRuleFlowContractProvider(serviceProvider);
    }

    public sealed class TestContext
    {
        public TestRequest Request { get; init; } = new();
    }

    public sealed class TestRequest
    {
        public decimal Amount { get; init; }
    }

    public sealed class FlowPrimaryRule : IRule<TestContext>
    {
        public string Code => "FLOW_TEST_PRIMARY";

        public Task<RuleResult> EvaluateAsync(TestContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }
    }
}
