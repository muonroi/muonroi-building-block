namespace Muonroi.Mediator.Mediator;

public readonly struct Unit
{
    public static readonly Unit Value = new();
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);
}
