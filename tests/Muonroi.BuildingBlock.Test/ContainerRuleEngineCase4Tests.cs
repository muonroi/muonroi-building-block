using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class ContainerRuleEngineCase4Tests
{
    private sealed record ContainerContext(string ContainerId);

    public interface IContainerGrpcClient
    {
        Task<bool> IsAtPortAsync(string id);
        Task<bool> IsValidForDepartureAsync(string id);
    }

    public interface ISealRestClient
    {
        Task<bool> HasSealAsync(string id);
    }

    public interface IRfidRestClient
    {
        Task SendToRfidAsync(string id);
    }

    public interface IRollbackService
    {
        Task RollbackAsync(string id);
    }

    private sealed class CheckAtPortRule(IContainerGrpcClient client) : IRule<ContainerContext>
    {
        public string Name => nameof(CheckAtPortRule);
        public IEnumerable<Type> Dependencies => System.Type.EmptyTypes;

        public string Code => nameof(CheckAtPortRule);

        public int Order => 1;

        public IReadOnlyList<string> DependsOn => [];

        public HookPoint HookPoint => HookPoint.BeforePersist;

        public RuleType Type => RuleType.Validation;

        public async Task<RuleResult> EvaluateAsync(ContainerContext context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            bool ok = await client.IsAtPortAsync(context.ContainerId);
            return ok ? RuleResult.Passed() : RuleResult.Failure("not at port");
        }

        public Task ExecuteAsync(ContainerContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CheckSealRule(ISealRestClient client) : IRule<ContainerContext>
    {
        public string Name => nameof(CheckSealRule);
        public IEnumerable<Type> Dependencies => [typeof(CheckAtPortRule)];

        public string Code => nameof(CheckSealRule);

        public int Order => 2;

        public IReadOnlyList<string> DependsOn => [nameof(CheckAtPortRule)];

        public HookPoint HookPoint => HookPoint.BeforePersist;

        public RuleType Type => RuleType.Validation;

        public async Task<RuleResult> EvaluateAsync(ContainerContext context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            bool ok = await client.HasSealAsync(context.ContainerId);
            return ok ? RuleResult.Passed() : RuleResult.Failure("seal open");
        }

        public Task ExecuteAsync(ContainerContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ValidateContainerRule(IContainerGrpcClient client) : IRule<ContainerContext>
    {
        public string Name => nameof(ValidateContainerRule);
        public IEnumerable<Type> Dependencies => [typeof(CheckSealRule)];

        public string Code => nameof(ValidateContainerRule);

        public int Order => 3;

        public IReadOnlyList<string> DependsOn => [nameof(CheckSealRule)];

        public HookPoint HookPoint => HookPoint.BeforePersist;

        public RuleType Type => RuleType.Validation;

        public async Task<RuleResult> EvaluateAsync(ContainerContext context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            bool ok = await client.IsValidForDepartureAsync(context.ContainerId);
            if (!ok) throw new InvalidOperationException("invalid container");
            return RuleResult.Passed();
        }

        public Task ExecuteAsync(ContainerContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class SendToRfidRule(IRfidRestClient client) : IRule<ContainerContext>
    {
        public string Name => nameof(SendToRfidRule);
        public IEnumerable<Type> Dependencies => [typeof(ValidateContainerRule)];

        public string Code => nameof(SendToRfidRule);

        public int Order => 4;

        public IReadOnlyList<string> DependsOn => [nameof(ValidateContainerRule)];

        public HookPoint HookPoint => HookPoint.BeforePersist;

        public RuleType Type => RuleType.Validation;

        public async Task<RuleResult> EvaluateAsync(ContainerContext context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            await client.SendToRfidAsync(context.ContainerId);
            return RuleResult.Passed();
        }

        public Task ExecuteAsync(ContainerContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RollbackHook(IRollbackService service) : IHookHandler<ContainerContext>
    {
        public async Task HandleAsync(
            HookPoint point,
            IRule<ContainerContext> rule,
            RuleResult result,
            FactBag facts,
            ContainerContext context,
            TimeSpan? duration = null,
            CancellationToken cancellationToken = default)
        {
            if (point == HookPoint.Error && rule is ValidateContainerRule)
                await service.RollbackAsync(context.ContainerId);
        }
    }

    [Fact]
    public async Task SuccessfulFlow_TriggersBarrier()
    {
        ContainerContext ctx = new("C1");
        IContainerGrpcClient grpc = Substitute.For<IContainerGrpcClient>();
        grpc.IsAtPortAsync("C1").Returns(true);
        grpc.IsValidForDepartureAsync("C1").Returns(true);
        ISealRestClient seal = Substitute.For<ISealRestClient>();
        seal.HasSealAsync("C1").Returns(true);
        IRfidRestClient rfid = Substitute.For<IRfidRestClient>();
        IRollbackService rollback = Substitute.For<IRollbackService>();

        IRule<ContainerContext>[] rules =
        [
            new CheckAtPortRule(grpc),
            new CheckSealRule(seal),
            new ValidateContainerRule(grpc),
            new SendToRfidRule(rfid)
        ];
        IHookHandler<ContainerContext>[] hooks = [new RollbackHook(rollback)];
        RuleOrchestrator<ContainerContext> orchestrator =
            new(rules, hooks, NullLogger<RuleOrchestrator<ContainerContext>>.Instance);

        await orchestrator.ExecuteAsync(ctx);

        await rfid.Received(1).SendToRfidAsync("C1");
        await rollback.DidNotReceive().RollbackAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ValidateFails_RollsBackAndStops()
    {
        ContainerContext ctx = new("C1");
        IContainerGrpcClient grpc = Substitute.For<IContainerGrpcClient>();
        grpc.IsAtPortAsync("C1").Returns(true);
        grpc.IsValidForDepartureAsync("C1").Returns(false);
        ISealRestClient seal = Substitute.For<ISealRestClient>();
        seal.HasSealAsync("C1").Returns(true);
        IRfidRestClient rfid = Substitute.For<IRfidRestClient>();
        IRollbackService rollback = Substitute.For<IRollbackService>();

        IRule<ContainerContext>[] rules =
        [
            new CheckAtPortRule(grpc),
            new CheckSealRule(seal),
            new ValidateContainerRule(grpc),
            new SendToRfidRule(rfid)
        ];
        IHookHandler<ContainerContext>[] hooks = [new RollbackHook(rollback)];
        RuleOrchestrator<ContainerContext> orchestrator =
            new(rules, hooks, NullLogger<RuleOrchestrator<ContainerContext>>.Instance);

        await Assert.ThrowsAsync<MInternalException>(() => orchestrator.ExecuteAsync(ctx));
        await rollback.Received(1).RollbackAsync("C1");
        await rfid.DidNotReceive().SendToRfidAsync(Arg.Any<string>());
    }
}
