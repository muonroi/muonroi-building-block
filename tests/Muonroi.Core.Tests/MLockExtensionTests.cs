namespace Muonroi.Core.Tests;

public class MLockExtensionTests
{
    [Fact]
    public void Locking_Action_Executes()
    {
        object source = new();
        bool executed = false;

        source.Locking(() => executed = true);

        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Locking_Prevents_Race_Condition()
    {
        object source = new();
        int count = 0;

        Task[] tasks = [.. Enumerable.Range(0, 100).Select(_ => Task.Run(() => source.Locking(() => count++)))];
        await Task.WhenAll(tasks);

        count.Should().Be(100);
    }

    [Fact]
    public void Locking_Generic_Func_Returns_Value()
    {
        List<int> list = [];

        int result = list.Locking(items =>
        {
            items.Add(3);
            return items[0];
        });

        result.Should().Be(3);
    }
}
