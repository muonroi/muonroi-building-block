namespace Muonroi.Observability.Logging;

/// <summary>
/// Defines bootstrap behavior for logging initialization.
/// </summary>
public enum BootstrapMethod
{
    /// <summary>Suppress bootstrap logging.</summary>
    Silent,
    /// <summary>Log failures during bootstrap.</summary>
    Failure,
    /// <summary>No special bootstrap behavior.</summary>
    None
}
