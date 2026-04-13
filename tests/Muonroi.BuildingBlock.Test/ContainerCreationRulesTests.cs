using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.BuildingBlock.Test;

public class ContainerCreationRulesTests
{
    public interface IContainerGateway
    {
        Task<bool> ContainerExistsAsync(string id);
        Task<bool> HasExitedPortAsync(string id);
        Task<bool> IsDeclaredElsewhereAsync(string id);
    }

    private sealed record ContainerContext(string Id)
    {
        public bool OwnershipCheckRequired { get; set; }
    }

    private sealed class ContainerExistsRule(IContainerGateway gateway) : IRule<ContainerContext>
    {
        public const string RuleCode = "ContainerExists";
        public string Code => RuleCode;
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;

        public IEnumerable<Type> Dependencies => [];

        public async Task<RuleResult> EvaluateAsync(ContainerContext ctx, FactBag facts, CancellationToken ct)
        {
            return await gateway.ContainerExistsAsync(ctx.Id)
                ? RuleResult.Success()
                : RuleResult.Failure("Container not found");
        }

        public Task ExecuteAsync(ContainerContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ContainerExitRule(IContainerGateway gateway) : IRule<ContainerContext>
    {
        public const string RuleCode = "ContainerExit";
        public string Code => RuleCode;
        public int Order => 2;
        public IReadOnlyList<string> DependsOn => [ContainerExistsRule.RuleCode];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;

        public IEnumerable<Type> Dependencies => [typeof(ContainerExistsRule)];

        public async Task<RuleResult> EvaluateAsync(ContainerContext ctx, FactBag facts, CancellationToken ct)
        {
            return await gateway.HasExitedPortAsync(ctx.Id)
                ? RuleResult.Failure("Container already left port")
                : RuleResult.Success();
        }

        public Task ExecuteAsync(ContainerContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ContainerOwnershipRule(IContainerGateway gateway) : IRule<ContainerContext>
    {
        public const string RuleCode = "OwnershipCheck";
        public string Code => RuleCode;
        public int Order => 3;
        public IReadOnlyList<string> DependsOn => [ContainerExistsRule.RuleCode];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;

        public IEnumerable<Type> Dependencies => [typeof(ContainerExistsRule)];

        public async Task<RuleResult> EvaluateAsync(ContainerContext ctx, FactBag facts, CancellationToken ct)
        {
            if (await gateway.IsDeclaredElsewhereAsync(ctx.Id))
            {
                ctx.OwnershipCheckRequired = true;
            }

            return RuleResult.Success();
        }

        public Task ExecuteAsync(ContainerContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private static RuleOrchestrator<ContainerContext> BuildOrchestrator(IContainerGateway gateway)
    {
        IRule<ContainerContext>[] rules =
        [
            new ContainerExistsRule(gateway),
            new ContainerExitRule(gateway),
            new ContainerOwnershipRule(gateway)
        ];

        ILogger<RuleOrchestrator<ContainerContext>> logger = NullLogger<RuleOrchestrator<ContainerContext>>.Instance;
        return new RuleOrchestrator<ContainerContext>(rules, [], logger);
    }

    [Fact]
    public async Task Stops_When_Container_Not_Found()
    {
        Mock<IContainerGateway> mock = new();
        mock.Setup(m => m.ContainerExistsAsync("C1")).ReturnsAsync(false);

        RuleOrchestrator<ContainerContext> orchestrator = BuildOrchestrator(mock.Object);
        ContainerContext context = new("C1");

        await Assert.ThrowsAsync<MInternalException>(() => orchestrator.ExecuteAsync(context));

        mock.Verify(m => m.HasExitedPortAsync(It.IsAny<string>()), Times.Never);
        mock.Verify(m => m.IsDeclaredElsewhereAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Blocks_When_Container_Left_Port()
    {
        Mock<IContainerGateway> mock = new();
        mock.Setup(m => m.ContainerExistsAsync("C1")).ReturnsAsync(true);
        mock.Setup(m => m.HasExitedPortAsync("C1")).ReturnsAsync(true);

        RuleOrchestrator<ContainerContext> orchestrator = BuildOrchestrator(mock.Object);
        ContainerContext context = new("C1");

        await Assert.ThrowsAsync<MInternalException>(() => orchestrator.ExecuteAsync(context));

        mock.Verify(m => m.IsDeclaredElsewhereAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Flags_Ownership_When_Previously_Declared()
    {
        Mock<IContainerGateway> mock = new();
        mock.Setup(m => m.ContainerExistsAsync("C1")).ReturnsAsync(true);
        mock.Setup(m => m.HasExitedPortAsync("C1")).ReturnsAsync(false);
        mock.Setup(m => m.IsDeclaredElsewhereAsync("C1")).ReturnsAsync(true);

        RuleOrchestrator<ContainerContext> orchestrator = BuildOrchestrator(mock.Object);
        ContainerContext context = new("C1");

        await orchestrator.ExecuteAsync(context);

        Assert.True(context.OwnershipCheckRequired);
    }
}
