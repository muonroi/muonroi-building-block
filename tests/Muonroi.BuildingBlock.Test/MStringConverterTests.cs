namespace Muonroi.BuildingBlock.Test;

public class MStringConverterTests
{
    [Fact]
    public void Constructor_Creates_Instance()
    {
        MStringConverter converter = new();
        Assert.NotNull(converter);
    }

    [Fact]
    public void ConvertFromProvider_Trims_Value()
    {
        MStringConverter converter = new();
        string result = converter.ConvertFromProviderExpression.Compile().Invoke(" test ");
        Assert.Equal("test", result);
    }

    [Fact]
    public void ConvertFromProvider_Null_Throws()
    {
        MStringConverter converter = new();
        Assert.Throws<NullReferenceException>(() => converter.ConvertFromProviderExpression.Compile().Invoke(null!));
    }
}
