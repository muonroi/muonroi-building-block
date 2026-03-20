namespace Muonroi.RuleEngine.CEP;

/// <summary>
/// Simple benchmark utilities comparing streaming versus batch processing.
/// </summary>
public static class CepBenchmark
{
    /// <summary>
    /// Runs a simple streaming vs batch timing comparison.
    /// </summary>
    public static (TimeSpan Stream, TimeSpan Batch) Run(int count)
    {
        DateTime start = DateTime.UtcNow; // MBB001-exempt: static-class boundary
        List<CepEvent<int>> events =
            [.. Enumerable.Range(0, count).Select(i => new CepEvent<int>("k", start.AddSeconds(i), i))];

        CepEngine<int> streamEngine = new(TimeSpan.FromSeconds(10), WindowType.Sliding);
        System.Diagnostics.Stopwatch swStream = System.Diagnostics.Stopwatch.StartNew();
        foreach (CepEvent<int> e in events) streamEngine.AddEvent(e.Key, e.Value, e.Timestamp);
        swStream.Stop();

        System.Diagnostics.Stopwatch swBatch = System.Diagnostics.Stopwatch.StartNew();
        _ = events
            .Where(e => e.Timestamp > events[^1].Timestamp - TimeSpan.FromSeconds(10))
            .ToList();
        swBatch.Stop();

        return (swStream.Elapsed, swBatch.Elapsed);
    }
}
