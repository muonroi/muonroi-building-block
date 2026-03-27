using Muonroi.Mediator.Behaviours;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Abstractions.Telemetry;

namespace Muonroi.Mediator.Tests.Behaviours;

/// <summary>
/// Verifies structured logging and telemetry behavior for <see cref="MRuleEngineBehavior{TRequest,TResponse}"/>.
/// </summary>
public class MRuleEngineBehaviorLoggingTests
{
    /// <summary>
    /// Ensures successful rule executions log an informational entry with structured scope data.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRulePasses_LogsInfoWithScopeAndElapsedTime()
    {
        RuleBehaviorFixture<TestRuleContext> fixture = new();
        fixture.AddRule(new SlowPassingRule());
        MRuleEngineBehavior<TestRuleRequest, string> behavior = new(fixture.ServiceFactory, fixture.Logger, executionContextAccessor: fixture.ExecutionContextAccessor);

        await behavior.Handle(new TestRuleRequest("ORD-1"), () => Task.FromResult("ok"), CancellationToken.None);

        LogEntry entry = fixture.Logger.Entries.Single(x => x.Level == LogLevel.Information && x.MessageTemplate.Contains("PASS", StringComparison.Ordinal));
        entry.Args[0].Should().Be("SLOW_PASS");
        entry.Scope.Should().NotBeNull();
        GetScopeValue<string>(entry.Scope!, "RuleCode").Should().Be("SLOW_PASS");
        GetScopeValue<string>(entry.Scope!, "HookPoint").Should().Be(HookPoint.BeforeRule.ToString());
        GetScopeValue<bool>(entry.Scope!, "Passed").Should().BeTrue();
        GetScopeValue<string?>(entry.Scope!, "TenantId").Should().Be("tenant-a");
        GetScopeValue<long>(entry.Scope!, "ElapsedMs").Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Ensures failed rule evaluations log a warning entry with structured scope data.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRuleFails_LogsWarnWithErrorsAndScope()
    {
        RuleBehaviorFixture<TestRuleContext> fixture = new();
        fixture.AddRule(new SlowFailingRule());
        MRuleEngineBehavior<TestRuleRequest, string> behavior = new(fixture.ServiceFactory, fixture.Logger, executionContextAccessor: fixture.ExecutionContextAccessor);

        await Assert.ThrowsAsync<MRuleViolationException>(() => behavior.Handle(new TestRuleRequest("ORD-2"), () => Task.FromResult("ok"), CancellationToken.None));

        LogEntry entry = fixture.Logger.Entries.Single(x => x.Level == LogLevel.Warning && x.MessageTemplate.Contains("FAIL", StringComparison.Ordinal));
        entry.Args[0].Should().Be("SLOW_FAIL");
        entry.Args[2].Should().Be("not-allowed");
        entry.Scope.Should().NotBeNull();
        GetScopeValue<string>(entry.Scope!, "RuleCode").Should().Be("SLOW_FAIL");
        GetScopeValue<bool>(entry.Scope!, "Passed").Should().BeFalse();
        GetScopeValue<string>(entry.Scope!, "HookPoint").Should().Be(HookPoint.BeforeRule.ToString());
        GetScopeValue<string?>(entry.Scope!, "TenantId").Should().Be("tenant-a");
        GetScopeValue<long>(entry.Scope!, "ElapsedMs").Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Ensures successful rule executions record the shared rule engine counter and duration histogram.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRulePasses_RecordsTelemetry()
    {
        RuleBehaviorFixture<TestRuleContext> fixture = new();
        fixture.AddRule(new SlowPassingRule());
        MRuleEngineBehavior<TestRuleRequest, string> behavior = new(fixture.ServiceFactory, fixture.Logger, executionContextAccessor: fixture.ExecutionContextAccessor);
        using MeterListener listener = CreateRuleExecutionListener(out List<MetricMeasurement> resultMeasurements, out List<MetricMeasurement> durationMeasurements);

        await behavior.Handle(new TestRuleRequest("ORD-3"), () => Task.FromResult("ok"), CancellationToken.None);

        MetricMeasurement resultMeasurement = FindSingleMeasurement(
            resultMeasurements,
            "SLOW_PASS",
            HookPoint.BeforeRule,
            passed: true);
        MetricMeasurement durationMeasurement = FindSingleMeasurement(
            durationMeasurements,
            "SLOW_PASS",
            HookPoint.BeforeRule);

        resultMeasurement.Value.Should().Be(1L);
        resultMeasurement.Tags["tenant_id"].Should().Be("tenant-a");
        resultMeasurement.Tags["phase"].Should().Be(HookPoint.BeforeRule.ToString());
        resultMeasurement.Tags["passed"].Should().Be(true);
        durationMeasurement.Value.Should().BeGreaterThan(0L);
    }

    /// <summary>
    /// Ensures failed rule evaluations record a failed execution outcome in telemetry.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRuleFails_RecordsFailedTelemetry()
    {
        RuleBehaviorFixture<TestRuleContext> fixture = new();
        fixture.AddRule(new SlowFailingRule());
        MRuleEngineBehavior<TestRuleRequest, string> behavior = new(fixture.ServiceFactory, fixture.Logger, executionContextAccessor: fixture.ExecutionContextAccessor);
        using MeterListener listener = CreateRuleExecutionListener(out List<MetricMeasurement> resultMeasurements, out List<MetricMeasurement> durationMeasurements);

        await Assert.ThrowsAsync<MRuleViolationException>(() => behavior.Handle(new TestRuleRequest("ORD-4"), () => Task.FromResult("ok"), CancellationToken.None));

        MetricMeasurement resultMeasurement = FindSingleMeasurement(
            resultMeasurements,
            "SLOW_FAIL",
            HookPoint.BeforeRule,
            passed: false);
        MetricMeasurement durationMeasurement = FindSingleMeasurement(
            durationMeasurements,
            "SLOW_FAIL",
            HookPoint.BeforeRule);

        resultMeasurement.Tags["rule_code"].Should().Be("SLOW_FAIL");
        resultMeasurement.Tags["passed"].Should().Be(false);
        durationMeasurement.Value.Should().BeGreaterThan(0L);
    }

    /// <summary>
    /// Reads a strongly typed property value from the structured log scope record.
    /// </summary>
    /// <typeparam name="TValue">The expected property type.</typeparam>
    /// <param name="scope">The captured scope instance.</param>
    /// <param name="propertyName">The property to read.</param>
    /// <returns>The extracted property value.</returns>
    private static TValue GetScopeValue<TValue>(object scope, string propertyName)
    {
        object? value = scope.GetType().GetProperty(propertyName)!.GetValue(scope);
        return (TValue)value!;
    }

    /// <summary>
    /// Finds the single measurement for a specific rule execution tag set.
    /// </summary>
    /// <param name="measurements">The captured measurements.</param>
    /// <param name="ruleCode">The expected rule code.</param>
    /// <param name="phase">The expected hook point.</param>
    /// <param name="passed">The optional expected outcome tag.</param>
    /// <returns>The matching measurement.</returns>
    private static MetricMeasurement FindSingleMeasurement(
        IEnumerable<MetricMeasurement> measurements,
        string ruleCode,
        HookPoint phase,
        bool? passed = null)
    {
        return measurements
            .Where(measurement => MatchesMeasurement(measurement, ruleCode, phase, passed))
            .Should()
            .ContainSingle()
            .Subject;
    }

    /// <summary>
    /// Determines whether a measurement matches the expected tag set.
    /// </summary>
    /// <param name="measurement">The measurement to inspect.</param>
    /// <param name="ruleCode">The expected rule code.</param>
    /// <param name="phase">The expected execution phase.</param>
    /// <param name="passed">The optional expected pass/fail flag.</param>
    /// <returns><c>true</c> when the tags match; otherwise <c>false</c>.</returns>
    private static bool MatchesMeasurement(
        MetricMeasurement measurement,
        string ruleCode,
        HookPoint phase,
        bool? passed)
    {
        if (!measurement.Tags.TryGetValue("rule_code", out object? measuredRuleCode) ||
            !string.Equals(Convert.ToString(measuredRuleCode), ruleCode, StringComparison.Ordinal))
        {
            return false;
        }

        if (!measurement.Tags.TryGetValue("phase", out object? measuredPhase) ||
            !string.Equals(Convert.ToString(measuredPhase), phase.ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        if (!passed.HasValue)
        {
            return true;
        }

        return measurement.Tags.TryGetValue("passed", out object? measuredPassed) &&
               measuredPassed is bool measuredPassedBoolean &&
               measuredPassedBoolean == passed.Value;
    }

    /// <summary>
    /// Creates a meter listener that captures the mediator rule execution counter and histogram.
    /// </summary>
    /// <param name="resultMeasurements">The captured counter measurements.</param>
    /// <param name="durationMeasurements">The captured duration measurements.</param>
    /// <returns>The configured listener.</returns>
    private static MeterListener CreateRuleExecutionListener(
        out List<MetricMeasurement> resultMeasurements,
        out List<MetricMeasurement> durationMeasurements)
    {
        List<MetricMeasurement> resultBuffer = [];
        List<MetricMeasurement> durationBuffer = [];

        MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == RuleEngineTelemetry.Meter.Name &&
                (instrument.Name == "rule_execution_result" || instrument.Name == "rule_eval_duration_ms"))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            MetricMeasurement entry = new(measurement, ToTagDictionary(tags));
            if (instrument.Name == "rule_execution_result")
            {
                resultBuffer.Add(entry);
                return;
            }

            if (instrument.Name == "rule_eval_duration_ms")
            {
                durationBuffer.Add(entry);
            }
        });
        listener.Start();
        resultMeasurements = resultBuffer;
        durationMeasurements = durationBuffer;
        return listener;
    }

    /// <summary>
    /// Materializes measurement tags into a lookup-friendly dictionary.
    /// </summary>
    /// <param name="tags">The measurement tag span.</param>
    /// <returns>A dictionary containing the measurement tags.</returns>
    private static Dictionary<string, object?> ToTagDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        Dictionary<string, object?> values = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> tag in tags)
        {
            values[tag.Key] = tag.Value;
        }

        return values;
    }

    /// <summary>
    /// Captured metric measurement used by the telemetry assertions.
    /// </summary>
    /// <param name="Value">The measured value.</param>
    /// <param name="Tags">The associated measurement tags.</param>
    private sealed record MetricMeasurement(long Value, IReadOnlyDictionary<string, object?> Tags);

    /// <summary>
    /// Passing rule that performs asynchronous work so timing is observable.
    /// </summary>
    private sealed class SlowPassingRule : IRule<TestRuleContext>
    {
        /// <inheritdoc/>
        public string Code => "SLOW_PASS";

        /// <inheritdoc/>
        public HookPoint HookPoint => HookPoint.BeforeRule;

        /// <inheritdoc/>
        public int Order => 0;

        /// <inheritdoc/>
        public async Task<RuleResult> EvaluateAsync(TestRuleContext ctx, FactBag facts, CancellationToken ct)
        {
            await Task.Delay(10, ct);
            return RuleResult.Passed();
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync(TestRuleContext context, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
        }
    }

    /// <summary>
    /// Failing rule that performs asynchronous work so timing is observable.
    /// </summary>
    private sealed class SlowFailingRule : IRule<TestRuleContext>
    {
        /// <inheritdoc/>
        public string Code => "SLOW_FAIL";

        /// <inheritdoc/>
        public HookPoint HookPoint => HookPoint.BeforeRule;

        /// <inheritdoc/>
        public int Order => 0;

        /// <inheritdoc/>
        public async Task<RuleResult> EvaluateAsync(TestRuleContext ctx, FactBag facts, CancellationToken ct)
        {
            await Task.Delay(10, ct);
            return RuleResult.Failure("not-allowed");
        }

        /// <inheritdoc/>
        public Task ExecuteAsync(TestRuleContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
