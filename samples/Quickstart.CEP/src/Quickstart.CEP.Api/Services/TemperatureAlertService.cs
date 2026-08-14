namespace Quickstart.CEP.Api.Services;

/// <summary>
/// Demonstrates direct use of <see cref="CepWindowBuilder"/> and
/// <see cref="CepEngine{T}"/> without the REST management layer.
///
/// Two patterns are shown:
///   1. <c>CepWindowBuilder.Named()</c> — build a <see cref="CepConfig"/> fluently,
///      then wire it to a typed payload via <c>CepWindowBuilder.For&lt;T&gt;(config)</c>.
///   2. <see cref="CepEngine{T}"/> directly — construct an engine with explicit
///      window settings and call <c>AddEvent</c> by hand.
/// </summary>
public sealed class TemperatureAlertService
{
    // -------------------------------------------------------------------------
    // Window 1: Sliding — temperature anomaly detection (5-minute lookback)
    // -------------------------------------------------------------------------
    // CepWindowBuilder.Named() returns CepConfigBuilder.
    // Chain: ForTenant → Sliding → KeepEventsFor → CorrelateBy → WithMetadata → Build()
    private static readonly CepConfig _slidingConfig =
        CepWindowBuilder
            .Named("temp-anomaly")
            .ForTenant("sensor-net")
            .Describe("Detects sustained temperature spikes using a 5-minute sliding window.")
            .Sliding(TimeSpan.FromMinutes(5))
            .KeepEventsFor(TimeSpan.FromMinutes(10))
            .CorrelateBy("deviceId")
            .WithMetadata("alert.threshold", "80")
            .WithMetadata("alert.min-events", "3")
            .Build();

    // -------------------------------------------------------------------------
    // Window 2: Tumbling — hourly aggregation bucket
    // -------------------------------------------------------------------------
    private static readonly CepConfig _tumblingConfig =
        CepWindowBuilder
            .Named("temp-hourly-agg")
            .ForTenant("sensor-net")
            .Describe("Groups temperature readings into non-overlapping 1-hour buckets.")
            .Tumbling(TimeSpan.FromHours(1))
            .CorrelateBy("deviceId")
            .WithMetadata("report.type", "hourly")
            .Build();

    // -------------------------------------------------------------------------
    // Runtime windows — bind CepConfig to SensorReading via CepWindowBuilder.For<T>
    // -------------------------------------------------------------------------
    // CepWindowBuilder.For<TPayload>(config) returns CepWindowRuntimeBuilder<TPayload>.
    // .CorrelateBy(Func<TPayload, string>) selects the key from the payload.
    // .Build() returns CepWindow<TPayload> which wraps a CepEngine internally.
    private readonly CepWindow<SensorReading> _slidingWindow =
        CepWindowBuilder
            .For<SensorReading>(_slidingConfig)
            .CorrelateBy(r => r.DeviceId)
            .Build();

    private readonly CepWindow<SensorReading> _tumblingWindow =
        CepWindowBuilder
            .For<SensorReading>(_tumblingConfig)
            .CorrelateBy(r => r.DeviceId)
            .Build();

    // -------------------------------------------------------------------------
    // Public config accessors — used by SensorController to return metadata
    // -------------------------------------------------------------------------
    public CepConfig SlidingConfig => _slidingConfig;
    public CepConfig TumblingConfig => _tumblingConfig;

    /// <summary>
    /// Pushes a <paramref name="reading"/> into both the sliding and tumbling
    /// windows and returns a summary of what each window currently holds.
    /// </summary>
    public WindowSummary RecordReading(SensorReading reading)
    {
        // CepWindow<T>.Add(payload, timestamp) → IReadOnlyList<CepEvent<T>>
        // The window applies the configured key selector (r => r.DeviceId) internally.
        IReadOnlyList<CepEvent<SensorReading>> slidingEvents =
            _slidingWindow.Add(reading, reading.Timestamp);

        IReadOnlyList<CepEvent<SensorReading>> tumblingEvents =
            _tumblingWindow.Add(reading, reading.Timestamp);

        return new WindowSummary(
            DeviceId: reading.DeviceId,
            SlidingWindowEvents: slidingEvents.Count,
            SlidingWindowAvg: slidingEvents.Count > 0
                ? slidingEvents.Average(e => e.Value.Value)
                : 0,
            TumblingWindowEvents: tumblingEvents.Count,
            TumblingWindowAvg: tumblingEvents.Count > 0
                ? tumblingEvents.Average(e => e.Value.Value)
                : 0);
    }

    /// <summary>
    /// Demonstrates using <see cref="CepEngine{T}"/> directly (without a
    /// <see cref="CepWindow{TPayload}"/> wrapper) to detect anomalies for a
    /// batch of readings and return any alerts that fired.
    /// </summary>
    public IReadOnlyList<AlertEvent> DetectAnomalies(
        IEnumerable<SensorReading> readings,
        double threshold,
        int minEvents)
    {
        // CepEngine<T> constructed with explicit window settings.
        CepEngine<SensorReading> engine = new(
            windowSize: TimeSpan.FromMinutes(5),
            windowType: WindowType.Sliding,
            ttl: TimeSpan.FromMinutes(10));

        List<AlertEvent> alerts = [];

        foreach (SensorReading reading in readings)
        {
            // CepEngine<T>.AddEvent(key, value, timestamp, configId?, tenantId?)
            // → returns events currently inside the active window for that key.
            IReadOnlyList<CepEvent<SensorReading>> windowEvents =
                engine.AddEvent(
                    key: reading.DeviceId,
                    value: reading,
                    timestamp: reading.Timestamp,
                    configId: _slidingConfig.Id,
                    tenantId: _slidingConfig.TenantId);

            if (windowEvents.Count >= minEvents)
            {
                double avg = windowEvents.Average(e => e.Value.Value);
                if (avg > threshold)
                {
                    alerts.Add(new AlertEvent(
                        DeviceId: reading.DeviceId,
                        Rule: $"avg-{reading.Metric}-exceeds-{threshold}",
                        AverageValue: Math.Round(avg, 2),
                        EventCount: windowEvents.Count,
                        WindowEnd: reading.Timestamp));
                }
            }
        }

        return alerts;
    }
}

/// <summary>
/// Snapshot of what both windows contain after a single reading is recorded.
/// </summary>
public sealed record WindowSummary(
    string DeviceId,
    int SlidingWindowEvents,
    double SlidingWindowAvg,
    int TumblingWindowEvents,
    double TumblingWindowAvg);
