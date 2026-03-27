namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Result of a connector execution. Output facts are written back to the FactBag.
/// </summary>
public sealed class ConnectorResult
{
    /// <summary>Indicates whether the connector execution succeeded.</summary>
    public bool Success { get; init; }
    /// <summary>Output facts produced by the connector.</summary>
    public Dictionary<string, object?> OutputFacts { get; init; } = new();
    /// <summary>Error message when the execution fails.</summary>
    public string? ErrorMessage { get; init; }
    /// <summary>Optional status code from the underlying connector call.</summary>
    public int? StatusCode { get; init; }
    /// <summary>Total execution time for the connector.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Creates a successful result.</summary>
    /// <param name="outputFacts">Optional output facts.</param>
    /// <param name="statusCode">Optional status code.</param>
    /// <param name="duration">Execution duration.</param>
    public static ConnectorResult Ok(Dictionary<string, object?>? outputFacts = null, int? statusCode = null, TimeSpan duration = default)
        => new() { Success = true, OutputFacts = outputFacts ?? new(), StatusCode = statusCode, Duration = duration };

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">Error message describing the failure.</param>
    /// <param name="statusCode">Optional status code.</param>
    /// <param name="duration">Execution duration.</param>
    public static ConnectorResult Fail(string error, int? statusCode = null, TimeSpan duration = default)
        => new() { Success = false, ErrorMessage = error, StatusCode = statusCode, Duration = duration };
}
