using Muonroi.Mediator.Behaviours;
using Muonroi.Mediator.Mediator.Attributes;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Mediator.Tests.Behaviours;

/// <summary>
/// Verifies notification emission behavior for <see cref="MRuleEngineBehavior{TRequest,TResponse}"/>.
/// </summary>
public class MRuleEngineBehaviorEmitTests
{
    /// <summary>
    /// Ensures a passing rule publishes its declared notification.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRulePasses_PublishesNotification()
    {
        RuleBehaviorFixture<TestRuleContext> fixture = new();
        fixture.AddRule(new EmittingRule());
        MRuleEngineBehavior<TestRuleRequest, string> behavior = new(fixture.ServiceFactory, fixture.Logger, executionContextAccessor: fixture.ExecutionContextAccessor);

        string response = await behavior.Handle(new TestRuleRequest("ORD-1"), () => Task.FromResult("ok"), CancellationToken.None);

        response.Should().Be("ok");
        fixture.Mediator.Published.Should().ContainSingle().Which.Should().BeOfType<TestNotification>();
    }

    /// <summary>
    /// Ensures failed rules do not emit notifications.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRuleFails_DoesNotPublishNotification()
    {
        RuleBehaviorFixture<TestRuleContext> fixture = new();
        fixture.AddRule(new FailingRule());
        MRuleEngineBehavior<TestRuleRequest, string> behavior = new(fixture.ServiceFactory, fixture.Logger, executionContextAccessor: fixture.ExecutionContextAccessor);

        await Assert.ThrowsAsync<MRuleViolationException>(() => behavior.Handle(new TestRuleRequest(string.Empty), () => Task.FromResult("ok"), CancellationToken.None));

        fixture.Mediator.Published.Should().BeEmpty();
    }

    /// <summary>
    /// Ensures factory-backed notifications are built from the rule context.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRuleImplementsFactory_PublishesFactoryNotification()
    {
        RuleBehaviorFixture<TestRuleContext> fixture = new();
        fixture.AddRule(new FactoryRule());
        MRuleEngineBehavior<TestRuleRequest, string> behavior = new(fixture.ServiceFactory, fixture.Logger, executionContextAccessor: fixture.ExecutionContextAccessor);

        await behavior.Handle(new TestRuleRequest("ORD-22"), () => Task.FromResult("ok"), CancellationToken.None);

        fixture.Mediator.Published.Should().ContainSingle();
        TestNotification notification = fixture.Mediator.Published.Single().Should().BeOfType<TestNotification>().Subject;
        notification.Payload.Should().Be("ORD-22");
    }

    /// <summary>
    /// Ensures all declared notifications are published when a rule passes.
    /// </summary>
    [Fact]
    public async Task Handle_WhenMultipleEmitAttributesExist_PublishesAllNotifications()
    {
        RuleBehaviorFixture<TestRuleContext> fixture = new();
        fixture.AddRule(new MultiEmitRule());
        MRuleEngineBehavior<TestRuleRequest, string> behavior = new(fixture.ServiceFactory, fixture.Logger, executionContextAccessor: fixture.ExecutionContextAccessor);

        await behavior.Handle(new TestRuleRequest("ORD-33"), () => Task.FromResult("ok"), CancellationToken.None);

        fixture.Mediator.Published.Should().HaveCount(2);
        fixture.Mediator.Published.Should().Contain(x => x is TestNotification);
        fixture.Mediator.Published.Should().Contain(x => x is SecondaryNotification);
    }

    /// <summary>
    /// Ensures rules without emit metadata do not publish notifications.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRuleHasNoEmitAttribute_DoesNotPublishNotification()
    {
        RuleBehaviorFixture<TestRuleContext> fixture = new();
        fixture.AddRule(new PassiveRule());
        MRuleEngineBehavior<TestRuleRequest, string> behavior = new(fixture.ServiceFactory, fixture.Logger, executionContextAccessor: fixture.ExecutionContextAccessor);

        await behavior.Handle(new TestRuleRequest("ORD-44"), () => Task.FromResult("ok"), CancellationToken.None);

        fixture.Mediator.Published.Should().BeEmpty();
    }

    /// <summary>
    /// Ensures notification publish failures are non-blocking for handler success.
    /// </summary>
    [Fact]
    public async Task Handle_WhenPublishFails_DoesNotAbortHandlerResponse()
    {
        RuleBehaviorFixture<TestRuleContext> fixture = new();
        fixture.Mediator.ThrowOnPublish = true;
        fixture.AddRule(new EmittingRule());
        MRuleEngineBehavior<TestRuleRequest, string> behavior = new(fixture.ServiceFactory, fixture.Logger, executionContextAccessor: fixture.ExecutionContextAccessor);

        string response = await behavior.Handle(new TestRuleRequest("ORD-55"), () => Task.FromResult("ok"), CancellationToken.None);

        response.Should().Be("ok");
        fixture.Logger.Entries.Should().Contain(x => x.Level == LogLevel.Error && x.MessageTemplate.Contains("notification emit failed", StringComparison.Ordinal));
    }

    /// <summary>
    /// Passing rule that emits a parameterless notification.
    /// </summary>
    [MEmitOnPass(typeof(TestNotification))]
    private sealed class EmittingRule : IRule<TestRuleContext>
    {
        /// <inheritdoc/>
        public string Code => "EMIT";

        /// <inheritdoc/>
        public HookPoint HookPoint => HookPoint.BeforeRule;

        /// <inheritdoc/>
        public int Order => 0;

        /// <inheritdoc/>
        public Task<RuleResult> EvaluateAsync(TestRuleContext ctx, FactBag facts, CancellationToken ct) => Task.FromResult(RuleResult.Passed());

        /// <inheritdoc/>
        public Task ExecuteAsync(TestRuleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Failing rule used to verify notifications are not emitted.
    /// </summary>
    private sealed class FailingRule : IRule<TestRuleContext>
    {
        /// <inheritdoc/>
        public string Code => "FAIL";

        /// <inheritdoc/>
        public HookPoint HookPoint => HookPoint.BeforeRule;

        /// <inheritdoc/>
        public int Order => 0;

        /// <inheritdoc/>
        public Task<RuleResult> EvaluateAsync(TestRuleContext ctx, FactBag facts, CancellationToken ct) => Task.FromResult(RuleResult.Failure("boom"));

        /// <inheritdoc/>
        public Task ExecuteAsync(TestRuleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Passing rule that builds its notification payload from the rule context.
    /// </summary>
    [MEmitOnPass(typeof(TestNotification))]
    private sealed class FactoryRule : IRule<TestRuleContext>, IRuleNotificationFactory<TestRuleContext>
    {
        /// <inheritdoc/>
        public string Code => "FACTORY";

        /// <inheritdoc/>
        public HookPoint HookPoint => HookPoint.BeforeRule;

        /// <inheritdoc/>
        public int Order => 0;

        /// <inheritdoc/>
        public Task<RuleResult> EvaluateAsync(TestRuleContext ctx, FactBag facts, CancellationToken ct) => Task.FromResult(RuleResult.Passed());

        /// <inheritdoc/>
        public Task ExecuteAsync(TestRuleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <inheritdoc/>
        public INotification BuildNotification(TestRuleContext context)
        {
            return new TestNotification { Payload = context.OrderId };
        }
    }

    /// <summary>
    /// Passing rule with multiple emit declarations.
    /// </summary>
    [MEmitOnPass(typeof(TestNotification))]
    [MEmitOnPass(typeof(SecondaryNotification))]
    private sealed class MultiEmitRule : IRule<TestRuleContext>
    {
        /// <inheritdoc/>
        public string Code => "MULTI";

        /// <inheritdoc/>
        public HookPoint HookPoint => HookPoint.BeforeRule;

        /// <inheritdoc/>
        public int Order => 0;

        /// <inheritdoc/>
        public Task<RuleResult> EvaluateAsync(TestRuleContext ctx, FactBag facts, CancellationToken ct) => Task.FromResult(RuleResult.Passed());

        /// <inheritdoc/>
        public Task ExecuteAsync(TestRuleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Passing rule with no emit metadata.
    /// </summary>
    private sealed class PassiveRule : IRule<TestRuleContext>
    {
        /// <inheritdoc/>
        public string Code => "PASSIVE";

        /// <inheritdoc/>
        public HookPoint HookPoint => HookPoint.BeforeRule;

        /// <inheritdoc/>
        public int Order => 0;

        /// <inheritdoc/>
        public Task<RuleResult> EvaluateAsync(TestRuleContext ctx, FactBag facts, CancellationToken ct) => Task.FromResult(RuleResult.Passed());

        /// <inheritdoc/>
        public Task ExecuteAsync(TestRuleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
