using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.Core.Tests;

public class MStringExtensionTests
{
    [Fact]
    public void ToBase64String_Null_And_Empty_Returns_Empty()
    {
        Assert.Equal(string.Empty, MStringExtension.ToBase64String(null!));
        Assert.Equal(string.Empty, string.Empty.ToBase64String());
    }

    [Fact]
    public void FromBase64String_Null_Or_Empty_Returns_Empty()
    {
        Assert.Equal(string.Empty, string.Empty.FromBase64String());

        string encoded = "dGVzdA==";
        Assert.Equal("test", encoded.FromBase64String());
    }

    [Fact]
    public void Truncate_Long_String_Returns_Truncated()
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
    public void Left_Length_Greater_Than_String_Throws()
    {
        Assert.Throws<MArgumentException>(() => "abc".Left(5));
    }

    [Fact]
    public void Left_Null_String_Throws()
    {
        Assert.Throws<MArgumentException>(() => MStringExtension.Left(null, 1));
    }

    [Fact]
    public void NormalizeString_Removes_Diacritics()
    {
        const string input = "\u0110\u1ea5T  N\u01b0\u1edbc ";

        string normalized = input.NormalizeString();

        Assert.Equal("datnuoc", normalized);
    }
}
