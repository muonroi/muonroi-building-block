namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Result of a connector execution. Output facts are written back to the FactBag.
/// </summary>
public sealed class ConnectorResult
{
    public bool Success { get; init; }
    public Dictionary<string, object?> OutputFacts { get; init; } = new();
    public string? ErrorMessage { get; init; }
    public int? StatusCode { get; init; }
    public TimeSpan Duration { get; init; }

    public static ConnectorResult Ok(Dictionary<string, object?>? outputFacts = null, int? statusCode = null, TimeSpan duration = default)
        => new() { Success = true, OutputFacts = outputFacts ?? new(), StatusCode = statusCode, Duration = duration };

    public static ConnectorResult Fail(string error, int? statusCode = null, TimeSpan duration = default)
        => new() { Success = false, ErrorMessage = error, StatusCode = statusCode, Duration = duration };
}
