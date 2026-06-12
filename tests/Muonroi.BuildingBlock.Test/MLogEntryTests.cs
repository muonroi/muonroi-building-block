

namespace Muonroi.BuildingBlock.Test;

public class MLogEntryTests
{
    [Fact]
    public void Identity_Default_NotNull()
    {
        MLogEntry entry = new();
        Assert.False(string.IsNullOrEmpty(entry.Identity));
    }

    [Fact]
    public void TransactionId_Get_Returns_Value()
    {
        MLogEntry entry = new()
        {
            TransactionId = "123"
        };
        Assert.Equal("123", entry.TransactionId);
        entry = new();
        Assert.Null(entry.TransactionId);
    }

    [Fact]
    public void SiteCode_Get_Returns_Value()
    {
        MLogEntry entry = new()
        {
            SiteCode = "S"
        };
        Assert.Equal("S", entry.SiteCode);
        entry = new();
        Assert.Null(entry.SiteCode);
    }
}
