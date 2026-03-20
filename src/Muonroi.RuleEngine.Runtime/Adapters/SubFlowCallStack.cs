using System.Collections.Immutable;

namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Tracks the active sub-flow call stack to detect circular references.
/// Uses AsyncLocal to propagate the stack through the async execution chain.
/// </summary>
internal static class SubFlowCallStack
{
    // MBB004-exempt: adapter-internal scope — AsyncLocal stays within Runtime package, not Abstractions
    private static readonly AsyncLocal<ImmutableStack<string>?> _stack = new();

    public static ImmutableStack<string> Current
        => _stack.Value ?? ImmutableStack<string>.Empty;

    /// <summary>
    /// Pushes <paramref name="workflowCode"/> onto the call stack.
    /// Throws <see cref="SubFlowCycleException"/> if the workflow is already on the stack.
    /// Returns a disposable scope that restores the previous stack on disposal.
    /// </summary>
    public static IDisposable Push(string workflowCode)
    {
        ImmutableStack<string>? prev = _stack.Value;
        if (prev?.Contains(workflowCode) == true)
        {
            throw new SubFlowCycleException(
                $"Circular sub-flow detected: '{workflowCode}' is already in the call stack.");
        }

        _stack.Value = (prev ?? ImmutableStack<string>.Empty).Push(workflowCode);
        return new PopScope(prev);
    }

    private sealed class PopScope : IDisposable
    {
        private readonly ImmutableStack<string>? _prev;

        public PopScope(ImmutableStack<string>? prev) { _prev = prev; }

        public void Dispose() { _stack.Value = _prev; }
    }
}

/// <summary>Thrown when a sub-flow execution would create a circular dependency.</summary>
public sealed class SubFlowCycleException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="SubFlowCycleException"/> class.</summary>
    /// <param name="message">Error message describing the cycle.</param>
    public SubFlowCycleException(string message) : base(message) { }
}
