using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class MLockExtensionTests
{
    [Fact]
    public void Locking_Action_Executes()
    {
        object obj = new();
        bool executed = false;
        obj.Locking(() => executed = true);
        Assert.True(executed);
    }

    [Fact]
    public void Locking_Null_Source_Throws()
    {
        object? obj = null;
        Assert.Throws<MArgumentException>(() => obj!.Locking(() => { }));
    }

    [Fact]
    public void Locking_Nested_Calls_Succeed()
    {
        object obj = new();
        int count = 0;
        obj.Locking(() => obj.Locking(() => count++));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Locking_Prevents_Race_Condition()
    {
        object obj = new();
        int count = 0;
        Task[] tasks = [.. Enumerable.Range(0, 100).Select(_ => Task.Run(() => obj.Locking(() => count++)))];
        await Task.WhenAll(tasks);
        Assert.Equal(100, count);
    }

    [Fact]
    public void Locking_Generic_Action_Executes()
    {
        StringBuilder builder = new();
        builder.Locking(b => b.Append('x'));
        Assert.Equal("x", builder.ToString());
    }

    [Fact]
    public void Locking_Func_Returns_Value()
    {
        object obj = new();
        int result = obj.Locking(() => 5);
        Assert.Equal(5, result);
    }

    [Fact]
    public void Locking_Generic_Func_Returns_Value()
    {
        List<int> list = [];
        int result = list.Locking(l =>
        {
            l.Add(3);
            return l[0];
        });
        Assert.Equal(3, result);
    }
}
