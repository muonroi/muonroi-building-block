using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Core.Abstractions.Context;

/// <summary>
/// Provides access to the current <see cref="ISystemExecutionContext"/>.
/// </summary>
public interface ISystemExecutionContextAccessor
{
    /// <summary>
    /// Gets the current system execution context.
    /// </summary>
    /// <returns>The current context, or <see cref="SystemExecutionContext.Empty"/> if none is set.</returns>
    ISystemExecutionContext Get();

    /// <summary>
    /// Sets the current system execution context.
    /// </summary>
    /// <param name="context">The context to set.</param>
    void Set(ISystemExecutionContext context);

    /// <summary>
    /// Clears the current system execution context.
    /// </summary>
    void Clear();
}

/// <summary>
/// A default implementation of <see cref="ISystemExecutionContextAccessor"/> using <see cref="AsyncLocal{T}"/>.
/// </summary>
public sealed class SystemExecutionContextAccessor : ISystemExecutionContextAccessor
{
    private static readonly AsyncLocal<ISystemExecutionContext?> Current = new();

    /// <summary>
    /// Gets the current system execution context.
    /// </summary>
    /// <returns>The current context, or <see cref="SystemExecutionContext.Empty"/> if none is set.</returns>
    public ISystemExecutionContext Get()
    {
        return Current.Value ?? SystemExecutionContext.Empty;
    }

    /// <summary>
    /// Sets the current system execution context.
    /// </summary>
    /// <param name="context">The context to set.</param>
    /// <exception cref="MArgumentException">Thrown if context is null.</exception>
    public void Set(ISystemExecutionContext context)
    {
        Current.Value = MGuard.NotNull(context);
    }

    /// <summary>
    /// Clears the current system execution context.
    /// </summary>
    public void Clear()
    {
        Current.Value = null;
    }
}
