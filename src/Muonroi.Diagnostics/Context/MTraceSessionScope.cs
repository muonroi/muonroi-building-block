using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Diagnostics;

namespace Muonroi.Diagnostics.Context;

/// <summary>
/// Ambient scope for the current trace session.
/// </summary>
public sealed class MTraceSessionScope : IDisposable
{
    private readonly ITraceSession? _previous;

    /// <summary>
    /// Creates a new scope for the provided session.
    /// </summary>
    public MTraceSessionScope(ITraceSession session)
    {
        _previous = MTraceContextHolder.Current.Value;
        MTraceContextHolder.Current.Value = session;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        MTraceContextHolder.Current.Value = _previous;
    }

    /// <summary>
    /// Gets the current trace session in scope.
    /// </summary>
    public static ITraceSession? Current => MTraceContextHolder.Current.Value;
}
