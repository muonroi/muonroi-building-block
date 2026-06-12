namespace Muonroi.BuildingBlock.Test;

public class UnitTests
{
    [Fact]
    public void StaticValue_Is_Singleton()
    {
        Unit[] results = new Unit[5];
        Parallel.For(0, results.Length, i => results[i] = Unit.Value);
        Assert.All(results, u => Assert.Equal(Unit.Value, u));
    }
}
