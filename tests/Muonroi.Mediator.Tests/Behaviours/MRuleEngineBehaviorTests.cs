namespace Muonroi.Mediator.Tests.Behaviours;

public class MRuleEngineBehaviorTests
{
    // ── Domain objects ─────────────────────────────────────────────────────────

    private sealed class OrderRuleContext(string orderId) : IRuleContext
    {
        public string OrderId { get; } = orderId;
        public void HaltGroup() { }
    }

    private record CreateOrderRequest(string OrderId, decimal Amount)
        : IMRuleRequest<string, OrderRuleContext>
    {
        public OrderRuleContext BuildRuleContext() => new(OrderId);
    }

    private class CreateOrderHandler : IRequestHandler<CreateOrderRequest, string>
    {
        public static bool Called { get; set; }
        public Task<string> Handle(CreateOrderRequest request, CancellationToken ct)
        {
            Called = true;
            return Task.FromResult($"created:{request.OrderId}");
        }
    }

    // ── Rules ──────────────────────────────────────────────────────────────────

    private class AmountPositiveRule : IRule<OrderRuleContext>
    {
        public string Code => "AMOUNT_POSITIVE";
        public int Order => 0;
        public HookPoint HookPoint => HookPoint.BeforeRule;

        public Task<RuleResult> EvaluateAsync(OrderRuleContext ctx, FactBag facts, CancellationToken ct)
            => Task.FromResult(RuleResult.Passed()); // passes always (amount checked by handler)

        public Task ExecuteAsync(OrderRuleContext ctx, CancellationToken ct) => Task.CompletedTask;
    }

    private class OrderIdNotEmptyRule : IRule<OrderRuleContext>
    {
        public string Code => "ORDERID_NOT_EMPTY";
        public int Order => 1;
        public HookPoint HookPoint => HookPoint.BeforeRule;

        public Task<RuleResult> EvaluateAsync(OrderRuleContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(string.IsNullOrWhiteSpace(ctx.OrderId)
                ? RuleResult.Failure("OrderId must not be empty")
                : RuleResult.Passed());
        }

        public Task ExecuteAsync(OrderRuleContext ctx, CancellationToken ct) => Task.CompletedTask;
    }

    private class PostAuditRule : IRule<OrderRuleContext>
    {
        public string Code => "POST_AUDIT";
        public static bool Executed { get; set; }
        public HookPoint HookPoint => HookPoint.AfterRule;
        public int Order => 0;

        public Task<RuleResult> EvaluateAsync(OrderRuleContext ctx, FactBag facts, CancellationToken ct)
            => Task.FromResult(RuleResult.Passed());

        public Task ExecuteAsync(OrderRuleContext ctx, CancellationToken ct)
        {
            Executed = true;
            return Task.CompletedTask;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IMediator BuildMediator(bool includeEmptyRule = false)
    {
        CreateOrderHandler.Called = false;
        PostAuditRule.Executed = false;

        ServiceCollection services = new();
        services.AddMMediator();

        services.AddTransient<
            IPipelineBehavior<CreateOrderRequest, string>, 
            MRuleEngineBehavior<CreateOrderRequest, string>>();

        services.AddTransient<IRequestHandler<CreateOrderRequest, string>, CreateOrderHandler>();
        services.AddTransient<IRule<OrderRuleContext>, AmountPositiveRule>();
        if (includeEmptyRule)
            services.AddTransient<IRule<OrderRuleContext>, OrderIdNotEmptyRule>();
        services.AddTransient<IRule<OrderRuleContext>, PostAuditRule>();

        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PreRules_Pass_HandlerExecuted()
    {
        IMediator mediator = BuildMediator();
        string result = await mediator.Send(new CreateOrderRequest("ORD-001", 100m));
        Assert.Equal("created:ORD-001", result);
        Assert.True(CreateOrderHandler.Called);
    }

    [Fact]
    public async Task PostRules_ExecutedAfterHandler()
    {
        IMediator mediator = BuildMediator();
        await mediator.Send(new CreateOrderRequest("ORD-002", 50m));
        Assert.True(PostAuditRule.Executed);
    }

    [Fact]
    public async Task PreRule_Failure_ThrowsRuleViolation_HandlerNotCalled()
    {
        IMediator mediator = BuildMediator(includeEmptyRule: true);
        MRuleViolationException ex = await Assert.ThrowsAsync<MRuleViolationException>(
            () => mediator.Send(new CreateOrderRequest("", 100m)));
        Assert.Equal("ORDERID_NOT_EMPTY", ex.RuleCode);
        Assert.False(CreateOrderHandler.Called);
    }

    [Fact]
    public async Task Request_WithoutIMRuleRequest_RuleEngineNotInvoked()
    {
        // A plain request goes through the behavior but skips all rule logic
        ServiceCollection services = new();
        services.AddMMediator();

        services.AddTransient<
            IPipelineBehavior<MediatorTests.PingRequest, string>, 
            MRuleEngineBehavior<MediatorTests.PingRequest, string>>();

        services.AddTransient<IRequestHandler<MediatorTests.PingRequest, string>>(
            _ => new PingHandlerStub());
        services.AddTransient<IRule<OrderRuleContext>>(
            _ => new PostAuditRule()); // rule is registered but should not fire

        PostAuditRule.Executed = false;
        IMediator mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        string result = await mediator.Send(new MediatorTests.PingRequest { Message = "x" });

        Assert.Equal("Pongx", result);
        Assert.False(PostAuditRule.Executed, "Rule engine should not fire for non-IMRuleRequest");
    }

    private class PingHandlerStub : IRequestHandler<MediatorTests.PingRequest, string>
    {
        public Task<string> Handle(MediatorTests.PingRequest request, CancellationToken ct)
            => Task.FromResult($"Pong{request.Message}");
    }
}
