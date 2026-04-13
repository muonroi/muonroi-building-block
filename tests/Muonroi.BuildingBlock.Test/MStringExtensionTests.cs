using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class MStringExtensionTests
{
    [Fact]
    public void ToBase64String_NullAndEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, MStringExtension.ToBase64String(null!));
        Assert.Equal(string.Empty, string.Empty.ToBase64String());
    }

    [Fact]
    public void FromBase64String_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, string.Empty.FromBase64String());
        string encoded = "dGVzdA==";
        Assert.Equal("test", encoded.FromBase64String());
    }

    [Fact]
    public void Truncate_LongString_ReturnsTruncated()
    {
        string result = "abcdef".Truncate(3)!;
        Assert.Equal("abc", result);
    }

    [Fact]
    public void Truncate_Null_Throws()
    {
        Assert.Throws<MArgumentException>(() => MStringExtension.Truncate(null, 1));
    }

    [Fact]
    public void Left_LengthGreaterThanString_Throws()
    {
        Assert.Throws<MArgumentException>(() => "abc".Left(5));
    }

    [Fact]
    public void Left_NullString_Throws()
    {
        Assert.Throws<MArgumentException>(() => MStringExtension.Left(null, 1));
    }

    [Fact]
    public void NormalizeString_RemovesDiacritics()
    {
        string input = "ĐấT  NướC ";
        string normalized = input.NormalizeString();
        Assert.Equal("datnuoc", normalized);
    }
}
