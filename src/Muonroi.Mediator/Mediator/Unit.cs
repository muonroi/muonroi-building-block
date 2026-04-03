namespace Muonroi.Mediator.Mediator;

/// <summary>
/// Represents the Unit.
/// </summary>
public readonly struct Unit
{
    /// <summary>
    /// Executes the Value operation.
    /// </summary>
    public static readonly Unit Value = new();
    /// <summary>
    /// Executes the Task operation.
    /// </summary>
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);
}
