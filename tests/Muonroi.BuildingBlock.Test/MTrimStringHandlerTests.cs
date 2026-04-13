namespace Muonroi.BuildingBlock.Test;

public class MTrimStringHandlerTests
{
    private class FakeParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => false;
#pragma warning disable CS8766
        public string? ParameterName { get; set; } = string.Empty;

        public string? SourceColumn { get; set; } = string.Empty;
#pragma warning restore CS8766
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }

    [Fact]
    public void Parse_Trims_String()
    {
        MTrimStringHandler handler = new();
        string? result = handler.Parse("  test ");
        Assert.Equal("test", result);
    }

    [Fact]
    public void Parse_Null_Throws()
    {
        MTrimStringHandler handler = new();
        Assert.Throws<NullReferenceException>(() => handler.Parse(null!));
    }

    [Fact]
    public void Parse_NonString_Returns_ToString_Trimmed()
    {
        MTrimStringHandler handler = new();
        string? result = handler.Parse(123);
        Assert.Equal("123", result);
    }

    [Fact]
    public void SetValue_Throws_NotImplemented()
    {
        MTrimStringHandler handler = new();
        FakeParameter p = new();
        Assert.Throws<NotImplementedException>(() => handler.SetValue(p, "v"));
    }
}
