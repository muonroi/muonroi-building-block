namespace Muonroi.Core.Abstractions.Diagnostics;

/// <summary>
/// Provides access to the current diagnostic trace session context.
/// </summary>
public interface IMTraceContext
{
    /// <summary>Returns the active trace session for the current async flow, or null.</summary>
    ITraceSession? Current { get; }

    /// <summary>Starts a new trace session and sets it as current.</summary>
    /// <param name="sessionId">The session identifier (usually CorrelationId).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="lineTraceEnabled">Whether deep line tracing is enabled.</param>
    /// <returns>A scope that restores the previous session when disposed.</returns>
    IDisposable Begin(string sessionId, string? tenantId, string? userId, bool lineTraceEnabled = false);
}
