using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Serialization;

namespace Muonroi.Diagnostics.Context;

/// <summary>
/// Default trace context implementation.
/// </summary>
public sealed class MTraceContext(IMJsonSerializeService json) : IMTraceContext
{
    /// <inheritdoc/>
    public ITraceSession? Current => MTraceSessionScope.Current;

    /// <inheritdoc/>
    public IDisposable Begin(string sessionId, string? tenantId, string? userId, bool lineTraceEnabled = false)
    {
        var session = new MTraceSession(sessionId, tenantId, userId, lineTraceEnabled, json);
        return new MTraceSessionScope(session);
    }
}
