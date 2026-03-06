namespace Muonroi.Logging;

internal sealed class MLogContextScope(IDisposable? inner) : IMLogContextScope
{
    private readonly IDisposable? _inner = inner;

    public void Dispose()
    {
        _inner?.Dispose();
    }
}
