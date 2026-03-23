using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Muonroi.RuleEngine.Runtime.Web.Controllers;
using Muonroi.RuleEngine.Runtime.Web.Models;
using Muonroi.RuleEngine.Runtime.Web.Services;
using NSubstitute;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class MRuleFlowContractControllerTests
{
    [Fact]
    public async Task MGetRuleContract_ShouldReturnOk_WhenProviderFindsContract()
    {
        IMRuleFlowContractProvider provider = Substitute.For<IMRuleFlowContractProvider>();
        MRuleFlowContractLookupResponse expected = new(
            "rule",
            "RULE_A",
            new MRuleContractSchema("RULE_A.Request", [new MRuleContractField("amount", "Amount", "number", true)]),
            null);
        provider.MGetContractAsync("rule", "RULE_A", Arg.Any<CancellationToken>())
            .Returns(expected);

        MRuleFlowContractController controller = new(provider);

        ActionResult<MRuleFlowContractLookupResponse> result = await controller.MGetRuleContract("rule", "RULE_A", default);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task MGetFlowContract_ShouldReturnNotFound_WhenProviderReturnsNull()
    {
        IMRuleFlowContractProvider provider = Substitute.For<IMRuleFlowContractProvider>();
        provider.MGetFlowContractAsync("wf-empty", Arg.Any<CancellationToken>())
            .Returns((MRuleFlowContractLookupResponse?)null);

        MRuleFlowContractController controller = new(provider);

        ActionResult<MRuleFlowContractLookupResponse> result = await controller.MGetFlowContract("wf-empty", default);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task MListFlowSummaries_ShouldReturnOkPayload()
    {
        IMRuleFlowContractProvider provider = Substitute.For<IMRuleFlowContractProvider>();
        IReadOnlyList<MRuleFlowSummary> flows =
        [
            new("wf-a", "Flow A", 2, 3),
            new("wf-b", "Flow B", null, 1)
        ];
        provider.MListFlowsAsync(Arg.Any<CancellationToken>())
            .Returns(flows);

        MRuleFlowContractController controller = new(provider);

        ActionResult<IReadOnlyList<MRuleFlowSummary>> result = await controller.MListFlowSummaries(default);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(flows);
    }
}
