namespace Muonroi.BuildingBlock.Test;

public class MJsonSerializeServiceTests
{
    private record SimpleObj(string Name, int Value);

    private record ComplexObj(string Id, SimpleObj Child);

    [Fact]
    public void Serialize_Object_Returns_Json_String()
    {
        MJsonSerializeService svc = new();
        string json = svc.Serialize(new SimpleObj("test", 1));
        Assert.Contains("\"Name\":\"test\"", json);
        Assert.Contains("\"Value\":1", json);
    }

    [Fact]
    public void Serialize_Null_Returns_Null_String()
    {
        MJsonSerializeService svc = new();
        string json = svc.Serialize<string?>(null);
        Assert.Equal("null", json);
    }

    [Fact]
    public void Serialize_Complex_Object_Success()
    {
        MJsonSerializeService svc = new();
        ComplexObj obj = new("id", new SimpleObj("child", 2));
        string json = svc.Serialize(obj);
        Assert.Contains("\"Id\":\"id\"", json);
        Assert.Contains("\"Child\":", json);
    }

    [Fact]
    public void Deserialize_Valid_String_Returns_Object()
    {
        MJsonSerializeService svc = new();
        string json = "{\"Name\":\"test\",\"Value\":3}";
        SimpleObj? result = svc.Deserialize<SimpleObj>(json);
        Assert.NotNull(result);
        Assert.Equal("test", result!.Name);
        Assert.Equal(3, result.Value);
    }

    [Fact]
    public void Deserialize_Null_String_Returns_Null()
    {
        MJsonSerializeService svc = new();
        string? text = null;
        Assert.Throws<ArgumentNullException>(() => svc.Deserialize<SimpleObj>(text!));
    }

    [Fact]
    public void Deserialize_Invalid_String_Throws()
    {
        MJsonSerializeService svc = new();
        Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => svc.Deserialize<SimpleObj>("not_json"));
    }
}
