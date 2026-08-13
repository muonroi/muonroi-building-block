using FluentAssertions;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Abstractions.Adapters;
using Muonroi.RuleEngine.Runtime.Adapters;
using Muonroi.Templating.Scriban;
using NSubstitute;
using Scriban.Runtime;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class ScriptingRuleAdapterTests
{
    [Fact]
    public async Task JavaScriptRuleAdapter_WhenExpressionIsEmpty_ReturnsFailure()
    {
        JavaScriptRuleAdapter<TestContext> sut = new(
            "js-empty",
            "",
            outputFields: null,
            new TestProjector(),
            Substitute.For<IMLog<JavaScriptRuleAdapter<TestContext>>>());

        RuleResult result = await sut.EvaluateAsync(new TestContext(12, "vip"), new FactBag(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task JavaScriptRuleAdapter_WhenTruthy_WritesOutputFields()
    {
        JavaScriptRuleAdapter<TestContext> sut = new(
            "js-pass",
            "amount > 10 && tier === 'vip'",
            [
                new FeelOutputField { Path = "decision", ValueExpression = "'approved'" }
            ],
            new TestProjector(),
            Substitute.For<IMLog<JavaScriptRuleAdapter<TestContext>>>());
        FactBag facts = new();

        RuleResult result = await sut.EvaluateAsync(new TestContext(12, "vip"), facts, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        facts.Get<string>("decision").Should().Be("approved");
        facts.Get<string>("__node.js-pass.decision").Should().Be("approved");
    }

    [Fact]
    public async Task JavaScriptRuleAdapter_WhenExpressionThrows_ReturnsFailure()
    {
        TestLog<JavaScriptRuleAdapter<TestContext>> log = new();
        JavaScriptRuleAdapter<TestContext> sut = new(
            "js-throw",
            "missing(",
            outputFields: null,
            new TestProjector(),
            log);

        RuleResult result = await sut.EvaluateAsync(new TestContext(12, "vip"), new FactBag(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.Contains("JavaScript expression error", StringComparison.OrdinalIgnoreCase));
        log.WarningCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task LiquidRuleAdapter_WhenTemplateRendersText_StoresStringOutput()
    {
        LiquidRuleAdapter<TestContext> sut = new(
            "liquid-text",
            "Hello {{ customer_name }}",
            "text",
            "message",
            new TestProjector(),
            new MJsonSerializeService(),
            Substitute.For<IMLog<LiquidRuleAdapter<TestContext>>>(),
            new ScribanTemplateEngine());
        FactBag facts = new();

        RuleResult result = await sut.EvaluateAsync(new TestContext(20, "gold"), facts, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        facts.Get<string>("message").Should().Be("Hello Alice");
    }

    [Fact]
    public async Task LiquidRuleAdapter_WhenOutputIsJson_DeserializesPayload()
    {
        LiquidRuleAdapter<TestContext> sut = new(
            "liquid-json",
            "{ \"tier\": \"{{ customer_tier }}\", \"amount\": {{ amount }} }",
            "json",
            "payload",
            new TestProjector(),
            new MJsonSerializeService(),
            Substitute.For<IMLog<LiquidRuleAdapter<TestContext>>>(),
            new ScribanTemplateEngine());
        FactBag facts = new();

        RuleResult result = await sut.EvaluateAsync(new TestContext(42, "vip"), facts, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        JsonElement payload = facts.Get<JsonElement>("payload");
        payload.GetProperty("tier").GetString().Should().Be("vip");
        payload.GetProperty("amount").GetInt32().Should().Be(42);
    }

    [Fact]
    public async Task LiquidRuleAdapter_WhenFunctionProviderRegistered_UsesCustomFunction()
    {
        LiquidRuleAdapter<TestContext> sut = new(
            "liquid-fn",
            "{{ shout customer_name }}",
            "text",
            "message",
            new TestProjector(),
            new MJsonSerializeService(),
            Substitute.For<IMLog<LiquidRuleAdapter<TestContext>>>(),
            new ScribanTemplateEngine([new TestScribanFunctionProvider()]));
        FactBag facts = new();

        RuleResult result = await sut.EvaluateAsync(new TestContext(42, "vip"), facts, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        facts.Get<string>("message").Should().Be("ALICE!");
    }

    [Fact]
    public async Task LiquidRuleAdapter_WhenTemplateIsInvalid_ReturnsFailure()
    {
        TestLog<LiquidRuleAdapter<TestContext>> log = new();
        LiquidRuleAdapter<TestContext> sut = new(
            "liquid-invalid",
            "{{ if customer_name }}",
            "text",
            "message",
            new TestProjector(),
            new MJsonSerializeService(),
            log,
            new ScribanTemplateEngine());

        RuleResult result = await sut.EvaluateAsync(new TestContext(42, "vip"), new FactBag(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.Contains("Liquid render error", StringComparison.OrdinalIgnoreCase));
        log.WarningCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Phase31")]
    public async Task ScribanTimeout_ReturnsFailure()
    {
        // Scriban-native syntax: very large loop triggers LoopLimit (10_000) or 5-second CTS budget.
        // Either the LoopLimit or the CancellationToken will stop execution and produce Failure.
        string template = "{{ for i in 1..100000000 }}{{ i }}{{ end }}";
        LiquidRuleAdapter<TestContext> adapter = CreateLiquidAdapter("timeout-test", template);
        FactBag facts = new();
        Stopwatch stopwatch = Stopwatch.StartNew();

        RuleResult result = await adapter.EvaluateAsync(new TestContext(1, "standard"), facts, CancellationToken.None);

        stopwatch.Stop();
        result.IsSuccess.Should().BeFalse();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10)); // Must not hang
    }

    [Fact]
    [Trait("Category", "Phase31")]
    public async Task ScribanLoopLimit_StopsExecution()
    {
        // Scriban-native range syntax enforces LoopLimit (Liquid range syntax does not use StepLoop)
        string template = "{{ for i in 1..20000 }}x{{ end }}";
        LiquidRuleAdapter<TestContext> adapter = CreateLiquidAdapter("loop-test", template);
        FactBag facts = new();

        RuleResult result = await adapter.EvaluateAsync(new TestContext(1, "standard"), facts, CancellationToken.None);

        // Should fail because LoopLimit=10_000 is exceeded (ScriptRuntimeException is caught -> Failure)
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Phase33")]
    public async Task JavaScriptTimeout_ReturnsFailure()
    {
        // D-01: while(true) loop should be terminated by 5-second timeout enforced by Jint CancellationToken
        JavaScriptRuleAdapter<TestContext> adapter = new(
            "js-timeout",
            "while(true){}",
            outputFields: null,
            new TestProjector(),
            Substitute.For<IMLog<JavaScriptRuleAdapter<TestContext>>>());
        FactBag facts = new();
        Stopwatch stopwatch = Stopwatch.StartNew();

        RuleResult result = await adapter.EvaluateAsync(new TestContext(1, "standard"), facts, CancellationToken.None);

        stopwatch.Stop();
        result.IsSuccess.Should().BeFalse();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10)); // Must terminate within budget
    }

    [Fact]
    [Trait("Category", "Phase33")]
    public async Task JavaScriptStatementLimit_ReturnsFailure()
    {
        // D-02: expression with >10k statements should be stopped by MaxStatements(10_000)
        // Build a sequence of 20_000 variable assignments: "var a0=0; var a1=1; ... var a19999=19999;"
        string manyStatements = string.Join(" ", Enumerable.Range(0, 20_000).Select(i => $"var a{i}={i};"));
        JavaScriptRuleAdapter<TestContext> adapter = new(
            "js-stmtlimit",
            manyStatements,
            outputFields: null,
            new TestProjector(),
            Substitute.For<IMLog<JavaScriptRuleAdapter<TestContext>>>());
        FactBag facts = new();

        RuleResult result = await adapter.EvaluateAsync(new TestContext(1, "standard"), facts, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    private static LiquidRuleAdapter<TestContext> CreateLiquidAdapter(string code, string template)
    {
        return new LiquidRuleAdapter<TestContext>(
            code,
            template,
            "text",
            "output",
            new TestProjector(),
            new MJsonSerializeService(),
            Substitute.For<IMLog<LiquidRuleAdapter<TestContext>>>(),
            new ScribanTemplateEngine());
    }

    public sealed record TestContext(int Amount, string CustomerTier)
    {
        public string CustomerName => "Alice";
    }

    private sealed class TestProjector : IContextProjector<TestContext>
    {
        public IReadOnlyDictionary<string, object?> Project(TestContext context)
        {
            return new Dictionary<string, object?>
            {
                ["amount"] = context.Amount,
                ["customer_name"] = context.CustomerName,
                ["customer_tier"] = context.CustomerTier,
                ["tier"] = context.CustomerTier
            };
        }
    }

    private sealed class TestScribanFunctionProvider : IScribanFunctionProvider
    {
        public void Register(ScriptObject scriptObject)
        {
            scriptObject.Import("shout", new Func<string, string>(value => value.ToUpperInvariant() + "!"));
        }
    }

    private sealed class TestLog<T> : IMLog<T>
    {
        public int WarningCalls { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        public IMLogContextScope BeginProperty(string key, object? value) => new NullScope();
        public void Info(string messageTemplate, params object?[] args) { }
        public void Warn(string messageTemplate, params object?[] args) => WarningCalls++;
        public void Error(Exception? ex, string messageTemplate, params object?[] args) { }
        public void Debug(string messageTemplate, params object?[] args) { }
        public void InfoTrace(string messageTemplate, params object?[] args) { }
        public void InfoContext(string message, object? request = null, object? result = null, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string filePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0) { }
        public void ErrorContext(Exception ex, string message, object? request = null, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string filePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0) { }
        public void Audit(string action, string objectType, string objectId, bool isSuccess = true, string? previousStatus = null, string? newStatus = null, object? extraData = null, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string filePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0) { }
    }

    private sealed class NullScope : IMLogContextScope
    {
        public void Dispose() { }
    }
}
