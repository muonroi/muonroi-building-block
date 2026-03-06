namespace Muonroi.BuildingBlock.Test;

public class MProtobufTimestampHandlerTests
{
    private class FakeParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => false;
#pragma warning disable CS8767
        public string ParameterName { get; set; } = string.Empty;
        public string SourceColumn { get; set; } = string.Empty;
#pragma warning restore CS8767
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }

    [Fact]
    public void Parse_Returns_Timestamp()
    {
        DateTime now = DateTime.UtcNow;
        MProtobufTimestampHandler handler = new();
        Timestamp ts = handler.Parse(now);
        Assert.Equal(Timestamp.FromDateTime(DateTime.SpecifyKind(now, DateTimeKind.Utc)), ts);
    }

    [Fact]
    public void Parse_Null_Throws()
    {
        MProtobufTimestampHandler handler = new();
        Assert.Throws<NullReferenceException>(() => handler.Parse(null!));
    }

    [Fact]
    public void Parse_InvalidType_Throws()
    {
        MProtobufTimestampHandler handler = new();
        Assert.Throws<InvalidCastException>(() => handler.Parse("bad"));
    }

    [Fact]
    public void SetValue_Assigns_Value()
    {
        MProtobufTimestampHandler handler = new();
        FakeParameter p = new();
        Timestamp ts = Timestamp.FromDateTime(DateTime.UtcNow);
        handler.SetValue(p, ts);
        Assert.Equal(ts, p.Value);
    }

    [Fact]
    public void SetValue_Null_Sets_Null()
    {
        MProtobufTimestampHandler handler = new();
        FakeParameter p = new();
        handler.SetValue(p, null);
        Assert.Null(p.Value);
    }

    [Fact]
    public void SetValue_NullParameter_Throws()
    {
        MProtobufTimestampHandler handler = new();
        Assert.Throws<NullReferenceException>(() => handler.SetValue(null!, Timestamp.FromDateTime(DateTime.UtcNow)));
    }
}
