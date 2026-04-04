namespace Muonroi.AspNetCore.Tests.Middleware;

public class AmqpContextMiddlewareTests
{
    [Fact]
    public void AddHeaders_And_GetHeaderByKey_ReturnsStringValue()
    {
        var context = new AspNetCore.Middleware.AmqpContext();
        context.AddHeaders(new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        });

        context.GetHeaderByKey("key1").Should().Be("value1");
        context.GetHeaderByKey("key2").Should().Be("value2");
    }

    [Fact]
    public void GetHeaderByKey_ByteArray_ReturnsDecodedString()
    {
        var context = new AspNetCore.Middleware.AmqpContext();
        byte[] bytes = Encoding.Default.GetBytes("hello-bytes");
        context.AddHeaders(new Dictionary<string, object> { ["key"] = bytes });

        context.GetHeaderByKey("key").Should().Be("hello-bytes");
    }

    [Fact]
    public void GetHeaderByKey_NonExistentKey_ReturnsNull()
    {
        var context = new AspNetCore.Middleware.AmqpContext();
        context.GetHeaderByKey("missing").Should().BeNull();
    }

    [Fact]
    public void GetHeaderByKey_ObjectValue_ReturnsToString()
    {
        var context = new AspNetCore.Middleware.AmqpContext();
        context.AddHeaders(new Dictionary<string, object> { ["num"] = 42 });

        context.GetHeaderByKey("num").Should().Be("42");
    }

    [Fact]
    public void ClearHeaders_RemovesAllHeaders()
    {
        var context = new AspNetCore.Middleware.AmqpContext();
        context.AddHeaders(new Dictionary<string, object> { ["key"] = "value" });

        context.ClearHeaders();

        context.GetHeaderByKey("key").Should().BeNull();
    }

    [Fact]
    public void AddHeaders_OverwritesExistingKey()
    {
        var context = new AspNetCore.Middleware.AmqpContext();
        context.AddHeaders(new Dictionary<string, object> { ["key"] = "v1" });
        context.AddHeaders(new Dictionary<string, object> { ["key"] = "v2" });

        context.GetHeaderByKey("key").Should().Be("v2");
    }
}
