namespace Muonroi.AspNetCore.Tests.Filters;

public class AmqpContextTests
{
    [Fact]
    public void AddHeaders_And_GetHeaderByKey_ReturnsStringValue()
    {
        var context = new AspNetCore.Filters.AmqpContext();
        context.AddHeaders(new Dictionary<string, object>
        {
            ["correlation-id"] = "abc-123",
            ["tenant-id"] = "tenant-1"
        });

        context.GetHeaderByKey("correlation-id").Should().Be("abc-123");
        context.GetHeaderByKey("tenant-id").Should().Be("tenant-1");
    }

    [Fact]
    public void GetHeaderByKey_ByteArray_DecodesCorrectly()
    {
        var context = new AspNetCore.Filters.AmqpContext();
        byte[] bytes = Encoding.Default.GetBytes("encoded-value");
        context.AddHeaders(new Dictionary<string, object> { ["data"] = bytes });

        context.GetHeaderByKey("data").Should().Be("encoded-value");
    }

    [Fact]
    public void GetHeaderByKey_MissingKey_ReturnsNull()
    {
        var context = new AspNetCore.Filters.AmqpContext();
        context.GetHeaderByKey("nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetHeaderByKey_NumericValue_ReturnsToString()
    {
        var context = new AspNetCore.Filters.AmqpContext();
        context.AddHeaders(new Dictionary<string, object> { ["count"] = 99 });

        context.GetHeaderByKey("count").Should().Be("99");
    }

    [Fact]
    public void ClearHeaders_ClearsAllEntries()
    {
        var context = new AspNetCore.Filters.AmqpContext();
        context.AddHeaders(new Dictionary<string, object> { ["a"] = "1", ["b"] = "2" });

        context.ClearHeaders();

        context.GetHeaderByKey("a").Should().BeNull();
        context.GetHeaderByKey("b").Should().BeNull();
    }

    [Fact]
    public void GetHeaderByKey_NullValue_ReturnsNull()
    {
        var context = new AspNetCore.Filters.AmqpContext();
        context.AddHeaders(new Dictionary<string, object> { ["key"] = null! });

        context.GetHeaderByKey("key").Should().BeNull();
    }
}
