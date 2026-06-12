namespace Muonroi.BuildingBlock.Test;

public class NullScope : IDisposable
{
    public static NullScope Instance { get; } = new();
    private NullScope() { }
    public void Dispose() { }
}
