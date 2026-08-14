using DecisionTableModel = Muonroi.RuleEngine.DecisionTable.Models.DecisionTable;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class DecisionTableRuleAdapterTests
{
    [Fact]
    public async Task EvaluateAsync_WhenTableMissing_ReturnsFailure()
    {
        IDecisionTableStore store = Substitute.For<IDecisionTableStore>();
        store.GetByIdAsync("discounts", Arg.Any<CancellationToken>())
            .Returns((DecisionTableModel?)null);

        var sut = new DecisionTableRuleAdapter<OrderContext>(
            "dt-1",
            "discounts",
            store,
            Substitute.For<IDecisionTableExecutor>(),
            Substitute.For<IContextProjector<OrderContext>>(),
            Substitute.For<IMLog<DecisionTableRuleAdapter<OrderContext>>>());

        RuleResult result = await sut.EvaluateAsync(new OrderContext(), new FactBag(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_WhenNoRowMatches_AndFailOnNoMatchEnabled_ReturnsFailure()
    {
        DecisionTableModel table = CreateTable();
        IDecisionTableStore store = Substitute.For<IDecisionTableStore>();
        store.GetByIdAsync("discounts", Arg.Any<CancellationToken>()).Returns(table);

        IDecisionTableExecutor executor = Substitute.For<IDecisionTableExecutor>();
        executor.ExecuteAsync(table, Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(new DecisionTableExecutionResult { Matched = false, HitPolicy = HitPolicy.First });

        var sut = new DecisionTableRuleAdapter<OrderContext>(
            "dt-2",
            "discounts",
            store,
            executor,
            Substitute.For<IContextProjector<OrderContext>>(),
            Substitute.For<IMLog<DecisionTableRuleAdapter<OrderContext>>>());

        RuleResult result = await sut.EvaluateAsync(new OrderContext(), new FactBag(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.Contains("No decision table row matched", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_WhenNoRowMatches_AndFailOnNoMatchDisabled_ReturnsPassed()
    {
        DecisionTableModel table = CreateTable();
        IDecisionTableStore store = Substitute.For<IDecisionTableStore>();
        store.GetByIdAsync("discounts", Arg.Any<CancellationToken>()).Returns(table);

        IDecisionTableExecutor executor = Substitute.For<IDecisionTableExecutor>();
        executor.ExecuteAsync(table, Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(new DecisionTableExecutionResult { Matched = false, HitPolicy = HitPolicy.First });

        var sut = new DecisionTableRuleAdapter<OrderContext>(
            "dt-3",
            "discounts",
            store,
            executor,
            Substitute.For<IContextProjector<OrderContext>>(),
            Substitute.For<IMLog<DecisionTableRuleAdapter<OrderContext>>>(),
            failOnNoMatch: false);

        RuleResult result = await sut.EvaluateAsync(new OrderContext(), new FactBag(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WhenMatched_UsesFactBagPriorityAndWritesOutputs()
    {
        DecisionTableModel table = CreateTable();
        IDecisionTableStore store = Substitute.For<IDecisionTableStore>();
        store.GetByIdAsync("discounts", Arg.Any<CancellationToken>()).Returns(table);

        IReadOnlyDictionary<string, object?>? capturedInputs = null;
        IDecisionTableExecutor executor = Substitute.For<IDecisionTableExecutor>();
        executor.ExecuteAsync(table, Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedInputs = callInfo.Arg<IReadOnlyDictionary<string, object?>>();
                return new DecisionTableExecutionResult
                {
                    Matched = true,
                    HitPolicy = HitPolicy.First,
                    MatchedRowIds = ["row-1"],
                    Outputs =
                    [
                        new DecisionTableOutputRow
                        {
                            RowId = "row-1",
                            Outputs = new Dictionary<string, object?> { ["decision"] = "approve" }
                        }
                    ]
                };
            });

        IContextProjector<OrderContext> projector = Substitute.For<IContextProjector<OrderContext>>();
        projector.Project(Arg.Any<OrderContext>())
            .Returns(new Dictionary<string, object?> { ["amount"] = 100m, ["region"] = "VN" });

        FactBag facts = new();
        facts.Set("amount", 500m);

        var sut = new DecisionTableRuleAdapter<OrderContext>(
            "dt-4",
            "discounts",
            store,
            executor,
            projector,
            Substitute.For<IMLog<DecisionTableRuleAdapter<OrderContext>>>());

        RuleResult result = await sut.EvaluateAsync(new OrderContext(), facts, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedInputs.Should().NotBeNull();
        capturedInputs!["amount"].Should().Be(500m);
        capturedInputs["region"].Should().Be("VN");
        facts.Get<string>("decision").Should().Be("approve");
    }

    private static DecisionTableModel CreateTable()
    {
        return new DecisionTableModel
        {
            Id = "discounts",
            Name = "Discounts",
            InputColumns =
            [
                new DecisionTableColumn { Id = "c1", Name = "amount", Label = "Amount", DataType = "number" },
                new DecisionTableColumn { Id = "c2", Name = "region", Label = "Region", DataType = "string" }
            ],
            OutputColumns =
            [
                new DecisionTableColumn { Id = "o1", Name = "decision", Label = "Decision", DataType = "string" }
            ],
            Rows = []
        };
    }

    public sealed class OrderContext;
}
