namespace Muonroi.Core.Abstractions.Context;

public sealed class SystemExecutionContextScope : IDisposable
{
    private readonly ISystemExecutionContextAccessor _accessor;
    private readonly ISystemExecutionContext _previous;
    private bool _disposed;

    public SystemExecutionContextScope(ISystemExecutionContextAccessor accessor, ISystemExecutionContext context)
    {
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        _previous = accessor.Get();
        _accessor.Set(context ?? throw new ArgumentNullException(nameof(context)));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _accessor.Set(_previous);
        _disposed = true;
    }
}
