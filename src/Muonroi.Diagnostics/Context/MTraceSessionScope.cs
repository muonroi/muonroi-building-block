using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Diagnostics;

namespace Muonroi.Diagnostics.Context;

public sealed class MTraceSessionScope : IDisposable
{
    private readonly ITraceSession? _previous;

    public MTraceSessionScope(ITraceSession session)
    {
        _previous = MTraceContextHolder.Current.Value;
        MTraceContextHolder.Current.Value = session;
    }

    public void Dispose()
    {
        MTraceContextHolder.Current.Value = _previous;
    }

    public static ITraceSession? Current => MTraceContextHolder.Current.Value;
}
