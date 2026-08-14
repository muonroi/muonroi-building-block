namespace Muonroi.RuleEngine.CEP.Observability;

/// <summary>
/// OpenTelemetry sources and instruments for CEP operations.
/// </summary>
public static class CepMetrics
{
    /// <summary>
    /// Activity source name for CEP traces.
    /// </summary>
    public const string ActivitySourceName = "Muonroi.CEP";

    /// <summary>
    /// Meter name for CEP metrics.
    /// </summary>
    public const string MeterName = "Muonroi.CEP";

    /// <summary>
    /// Activity source used around CEP evaluation and repository calls.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    /// Meter used to publish CEP measurements.
    /// </summary>
    public static readonly Meter Meter = new(MeterName);

    /// <summary>
    /// Counts the number of events pushed into CEP windows.
    /// </summary>
    public static readonly Counter<long> EventsProcessed =
        Meter.CreateCounter<long>("cep.events.processed");

    /// <summary>
    /// Counts the number of window evaluations performed.
    /// </summary>
    public static readonly Counter<long> WindowEvaluations =
        Meter.CreateCounter<long>("cep.window.evaluations");

    /// <summary>
    /// Records the number of events currently present in a window.
    /// </summary>
    public static readonly Histogram<int> WindowEventCount =
        Meter.CreateHistogram<int>("cep.window.event.count");

    /// <summary>
    /// Counts repository read operations.
    /// </summary>
    public static readonly Counter<long> ConfigReads =
        Meter.CreateCounter<long>("cep.config.reads");

    /// <summary>
    /// Counts repository write operations.
    /// </summary>
    public static readonly Counter<long> ConfigWrites =
        Meter.CreateCounter<long>("cep.config.writes");
}
