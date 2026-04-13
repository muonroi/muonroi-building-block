using System.Diagnostics;
using System.Diagnostics.Metrics;
using Muonroi.RuleEngine.CEP.Observability;

namespace Muonroi.RuleEngine.CEP.Tests;

public class CepEngineTests
{
    [Fact]
    public void SlidingWindow_ReturnsEventsWithinWindow()
    {
        CepEngine<int> engine = new(TimeSpan.FromSeconds(10), WindowType.Sliding);
        DateTime t0 = new(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc);

        engine.AddEvent("a", 1, t0);
        IReadOnlyList<CepEvent<int>> result = engine.AddEvent("a", 2, t0.AddSeconds(15));

        Assert.Single(result);
        Assert.Equal(2, result[0].Value);
    }

    [Fact]
    public void TumblingWindow_GroupsIntoBuckets()
    {
        CepEngine<int> engine = new(TimeSpan.FromSeconds(10), WindowType.Tumbling);
        DateTime t0 = new(2026, 3, 9, 10, 0, 7, DateTimeKind.Utc);

        engine.AddEvent("a", 1, t0);
        IReadOnlyList<CepEvent<int>> bucket2 = engine.AddEvent("a", 2, t0.AddSeconds(15));

        Assert.Single(bucket2);
        Assert.Equal(2, bucket2[0].Value);
    }

    [Fact]
    public void Ttl_RemovesExpiredEvents()
    {
        CepEngine<int> engine = new(TimeSpan.FromSeconds(10), WindowType.Sliding, ttl: TimeSpan.FromSeconds(10));
        DateTime t0 = new(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc);

        engine.AddEvent("a", 1, t0);
        IReadOnlyList<CepEvent<int>> result = engine.AddEvent("a", 2, t0.AddSeconds(25));

        Assert.Single(result);
        Assert.Equal(2, result[0].Value);
    }

    [Fact]
    public void OutOfOrder_IsSortedByTimestamp()
    {
        CepEngine<int> engine = new(TimeSpan.FromSeconds(10), WindowType.Sliding);
        DateTime t0 = new(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc);

        engine.AddEvent("a", 1, t0.AddSeconds(5));
        engine.AddEvent("a", 2, t0);
        IReadOnlyList<CepEvent<int>> result = engine.AddEvent("a", 3, t0.AddSeconds(6));

        Assert.Equal(3, result.Count);
        Assert.True(result[0].Timestamp <= result[1].Timestamp && result[1].Timestamp <= result[2].Timestamp);
    }

    [Fact]
    public void Correlation_ByKeySeparatesEvents()
    {
        CepEngine<int> engine = new(TimeSpan.FromSeconds(10), WindowType.Sliding);
        DateTime t0 = new(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc);

        engine.AddEvent("a", 1, t0);
        IReadOnlyList<CepEvent<int>> resultB = engine.AddEvent("b", 2, t0.AddSeconds(5));

        Assert.Single(resultB);
        Assert.Equal("b", resultB[0].Key);
    }

    [Fact]
    public void AddEvent_EmitsCepActivity()
    {
        CepEngine<int> engine = new(TimeSpan.FromSeconds(10), WindowType.Sliding);
        Activity? started = null;

        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == CepMetrics.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => started = activity
        };

        ActivitySource.AddActivityListener(listener);

        engine.AddEvent("trace-key", 7, new DateTime(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc));

        Assert.NotNull(started);
        Assert.Equal("cep.window.evaluate", started!.OperationName);
    }

    [Fact]
    public void AddEvent_RecordsMetrics()
    {
        CepEngine<int> engine = new(TimeSpan.FromSeconds(10), WindowType.Sliding);
        long processed = 0;
        int lastWindowCount = 0;

        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == CepMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "cep.events.processed")
            {
                processed += measurement;
            }
        });
        listener.SetMeasurementEventCallback<int>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "cep.window.event.count")
            {
                lastWindowCount = measurement;
            }
        });
        listener.Start();

        DateTime t0 = new(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc);
        engine.AddEvent("m", 1, t0);
        engine.AddEvent("m", 2, t0.AddSeconds(1));

        Assert.Equal(2, processed);
        Assert.Equal(2, lastWindowCount);
    }
}
