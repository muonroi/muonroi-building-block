namespace Muonroi.Core.Tests;

public class JsonExtensionsTests
{
    private record SimpleObj(string Name, int Value);

    private record ComplexObj(string Id, SimpleObj Child);

    private class ThrowingObj
    {
        public string Name => throw new InvalidOperationException();
    }

    [Fact]
    public void Serialize_Object_Returns_Json()
    {
        SimpleObj obj = new("test", 1);

        string json = obj.Serialize();

        Assert.Contains("\"Name\":\"test\"", json);
        Assert.Contains("\"Value\":1", json);
    }

    [Fact]
    public void Serialize_Null_Returns_Null_String()
    {
        string json = JsonExtensions.Serialize(null!);

        Assert.Equal("null", json);
    }

    [Fact]
    public void Serialize_Complex_Object_Success()
    {
        ComplexObj obj = new("id", new SimpleObj("child", 2));

        string json = obj.Serialize();

        Assert.Contains("\"Id\":\"id\"", json);
        Assert.Contains("\"Child\"", json);
    }

    [Fact]
    public void Serialize_When_Exception_Returns_Empty()
    {
        string json = new ThrowingObj().Serialize();

        Assert.Equal(string.Empty, json);
    }

    [Fact]
    public async Task Serialize_Is_Thread_Safe()
    {
        SimpleObj obj = new("a", 1);
        Task<string>[] tasks = [.. Enumerable.Range(0, 10).Select(_ => Task.Run(() => obj.Serialize()))];

        await Task.WhenAll(tasks);

        foreach (string result in tasks.Select(t => t.Result))
        {
            Assert.Equal(obj.Serialize(), result);
        }
    }
}
