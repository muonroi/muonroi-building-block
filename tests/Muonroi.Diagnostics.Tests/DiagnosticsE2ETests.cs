using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Serialization;
using Muonroi.Diagnostics.Abstractions;
using Muonroi.Diagnostics.Extensions;
using Muonroi.Logging;
using Muonroi.Mediator.Behaviours;
using Muonroi.Mediator.Mediator;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core;
using System.Text;
using Xunit;

namespace Muonroi.Diagnostics.Tests;

public class DiagnosticsE2ETests
{
    private readonly IServiceCollection _services;

    public DiagnosticsE2ETests()
    {
        _services = new ServiceCollection();
        
        // 1. Core infrastructure
        _services.AddLogging(builder => builder.AddMuonroiLogging());
        _services.AddSingleton<IMJsonSerializeService, MJsonSerializeService>();
        _services.AddSingleton<ISystemExecutionContextAccessor>(sp => {
            var mock = new Mock<ISystemExecutionContextAccessor>();
            mock.Setup(x => x.Get()).Returns(new SystemExecutionContext(
                tenantId: "tenant-777",
                userId: "user-999",
                username: "gemini",
                correlationId: "trace-abc-123",
                accessToken: null,
                apiKey: null,
                isAuthenticated: true,
                permissions: [],
                sourceType: "unit-test"
            ));
            return mock.Object;
        });

        // 2. Diagnostics
        _services.AddMuonroiDiagnostics();

        // 3. Mediator with Diagnostics Behavior
        _services.AddMMediator(options => {
            // Register as open generics
            options.AddBehavior(typeof(MDiagnosticsBehavior<,>));
            options.AddBehavior(typeof(MRuleEngineBehavior<,>));
        });

        // 4. Rule Engine
        _services.AddTransient<RuleOrchestrator<OrderRequest>>();
        _services.AddTransient<IRule<OrderRequest>, MinimumAmountRule>();
        
        // 5. Mock HTTP Context for Header
        var mockHttp = new Mock<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Muonroi-Trace"] = "deep";
        mockHttp.Setup(x => x.HttpContext).Returns(context);
        _services.AddSingleton<IHttpContextAccessor>(mockHttp.Object);

        // 6. Handler
        _services.AddTransient<IRequestHandler<OrderRequest, string>, OrderHandler>();
    }

    [Fact]
    public async Task Complete_Flow_Should_Produce_Hierarchical_Trace_Tree()
    {
        // Arrange
        var sp = _services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        var store = sp.GetRequiredService<ITraceSessionStore>();
        var request = new OrderRequest("ORD-100", 250.5m);

        // Act
        var result = await mediator.Send(request);

        // Assert
        result.Should().Be("processed:ORD-100");

        // Verify Store
        MTraceSessionRecord? session = await store.GetAsync("trace-abc-123", "tenant-777");
        session.Should().NotBeNull();
        session!.SessionId.Should().Be("trace-abc-123");
        session.TenantId.Should().Be("tenant-777");

        // Verify Tree Structure
        session.Nodes.Should().HaveCount(1);
        var mediatorNode = session.Nodes[0];
        mediatorNode.Name.Should().Be(nameof(OrderRequest));
        mediatorNode.Type.Should().Be(MTraceNodeType.MediatorRequest);

        // Verify Mediator Request Body Capture
        mediatorNode.Events.Should().Contain(e => e.Message.Contains("Request body captured"));

        // Verify Rule Engine Node (Child of Mediator)
        var ruleEngineNode = mediatorNode.Children.Should().ContainSingle(n => n.Name == "RuleEngine").Which;
        ruleEngineNode.Type.Should().Be(MTraceNodeType.PipelineBehavior);

        // Verify Individual Rule Node (Sub-child of Rule Engine)
        var specificRuleNode = ruleEngineNode.Children.Should().ContainSingle(n => n.Name == "MIN_AMOUNT").Which;
        specificRuleNode.Type.Should().Be(MTraceNodeType.Rule);

        // Verify Rule Results/Snapshot
        specificRuleNode.InputFactsJson.Should().NotBeNullOrEmpty();
        specificRuleNode.DurationMs.Should().BeGreaterThan(0);
    }

    // --- Mocks & Domain Objects ---

    public record OrderRequest(string OrderId, decimal Amount) 
        : IMRuleRequest<string, OrderRequest>, IRequest<string>, IRuleContext
    {
        public OrderRequest BuildRuleContext() => this;
        public void HaltGroup() { }
    }

    public class OrderHandler : IRequestHandler<OrderRequest, string>
    {
        public Task<string> Handle(OrderRequest request, CancellationToken ct) => Task.FromResult($"processed:{request.OrderId}");
    }

    public class MinimumAmountRule : IRule<OrderRequest>
    {
        public string Code => "MIN_AMOUNT";
        public int Order => 0;
        public HookPoint HookPoint => HookPoint.BeforeRule;

        public Task<RuleResult> EvaluateAsync(OrderRequest ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(ctx.Amount >= 100 
                ? RuleResult.Passed() 
                : RuleResult.Failure("Amount too low"));
        }

        public Task ExecuteAsync(OrderRequest ctx, CancellationToken ct) => Task.CompletedTask;
    }

    // Standard JSON implementation for test
    public class MJsonSerializeService : IMJsonSerializeService
    {
        public string Serialize<T>(T obj) => System.Text.Json.JsonSerializer.Serialize(obj);
        public T? Deserialize<T>(string text) => System.Text.Json.JsonSerializer.Deserialize<T>(text);
    }
}
