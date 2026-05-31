namespace Muonroi.RuleEngine.Core.Tests;

public class RuleEngineTelemetryTests
{
    private sealed class DummyRule : IRule<string>
    {
        public string Name => "Dummy";
        public IEnumerable<Type> Dependencies => [];
        public string Code => "D1";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<RuleResult> EvaluateAsync(string ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }
    }

    [Fact]
    public async Task RulesFiredCounter_Increments_WhenRuleExecuted()
    {
        // NOTE: no OTLP exporter here. This test asserts the in-process "rules.fired"
        // counter increments, observed via the MeterListener below. Wiring an
        // AddOtlpExporter to localhost:4317 (no collector in CI/local) made the gRPC
        // exporter fail on flush/dispose and crashed the test host (empty error,
        // "VSTestTask returned false"). A MeterProvider listening to the meter is
        // enough to keep the instruments alive for the listener.
        using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("Muonroi.RuleEngine")
            .Build();

        long firedBefore = 0;
        long firedAfter = 0;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Muonroi.RuleEngine" && instrument.Name == "rules.fired")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => Interlocked.Add(ref firedAfter, measurement));
        listener.Start();

        // Capture baseline (other parallel tests may have already fired)
        firedBefore = Interlocked.Read(ref firedAfter);

        DummyRule rule = new();
        RuleOrchestrator<string> orchestrator = new([rule], [], null, Array.Empty<IRuleEventListener<string>>());
        await orchestrator.ExecuteAsync("ctx");

        listener.Dispose();
        meterProvider.ForceFlush();

        long delta = Interlocked.Read(ref firedAfter) - firedBefore;
        Assert.True(delta >= 1, $"Expected at least 1 rules.fired increment, got delta={delta}");
    }
}
