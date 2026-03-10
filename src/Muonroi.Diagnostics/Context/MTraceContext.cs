using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Serialization;

namespace Muonroi.Diagnostics.Context;

public sealed class MTraceContext(IMJsonSerializeService json) : IMTraceContext
{
    public ITraceSession? Current => MTraceSessionScope.Current;

    public IDisposable Begin(string sessionId, string? tenantId, string? userId, bool lineTraceEnabled = false)
    {
        var session = new MTraceSession(sessionId, tenantId, userId, lineTraceEnabled, json);
        return new MTraceSessionScope(session);
    }
}
