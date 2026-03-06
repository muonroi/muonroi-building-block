namespace Muonroi.Core.Abstractions.Context;

public interface ISystemExecutionContextAccessor
{
    ISystemExecutionContext Get();
    void Set(ISystemExecutionContext context);
    void Clear();
}

public sealed class SystemExecutionContextAccessor : ISystemExecutionContextAccessor
{
    private static readonly AsyncLocal<ISystemExecutionContext?> Current = new();

    public ISystemExecutionContext Get()
    {
        return Current.Value ?? SystemExecutionContext.Empty;
    }

    public void Set(ISystemExecutionContext context)
    {
        Current.Value = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void Clear()
    {
        Current.Value = null;
    }
}
