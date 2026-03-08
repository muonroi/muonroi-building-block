namespace Muonroi.Core.Abstractions.Context;

/// <summary>
/// A scope that temporarily sets the current system execution context.
/// When disposed, the previous context is restored.
/// </summary>
public sealed class SystemExecutionContextScope : IDisposable
{
    private readonly ISystemExecutionContextAccessor _accessor;
    private readonly ISystemExecutionContext _previous;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemExecutionContextScope"/> class.
    /// Sets the specified context as the current one.
    /// </summary>
    /// <param name="accessor">The execution context accessor.</param>
    /// <param name="context">The context to set for this scope.</param>
    /// <exception cref="ArgumentNullException">Thrown if accessor or context is null.</exception>
    public SystemExecutionContextScope(ISystemExecutionContextAccessor accessor, ISystemExecutionContext context)
    {
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        _previous = accessor.Get();
        _accessor.Set(context ?? throw new ArgumentNullException(nameof(context)));
    }

    /// <summary>
    /// Restores the previous execution context.
    /// </summary>
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
