namespace Muonroi.Logging;

/// <summary>
/// Implementation of <see cref="IMLogContextScope"/> that wraps an <see cref="IDisposable"/> scope.
/// </summary>
/// <param name="inner">The underlying disposable scope.</param>
internal sealed class MLogContextScope(IDisposable? inner) : IMLogContextScope
{
    private readonly IDisposable? _inner = inner;

    /// <inheritdoc />
    public void Dispose()
    {
        _inner?.Dispose();
    }
}
