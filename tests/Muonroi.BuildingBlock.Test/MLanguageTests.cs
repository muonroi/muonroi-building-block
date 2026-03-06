namespace Muonroi.BuildingBlock.Test;

public class MLanguageTests
{
    [Fact]
    public void Name_Get_Returns_Value()
    {
        MLanguage lang = new()
        {
            Name = "en"
        };
        Assert.Equal("en", lang.Name);
        lang = new MLanguage();
        Assert.Equal(string.Empty, lang.Name);
    }

    [Fact]
    public void DisplayName_Get_Returns_Value()
    {
        MLanguage lang = new()
        {
            DisplayName = "English"
        };
        Assert.Equal("English", lang.DisplayName);
        lang = new MLanguage();
        Assert.Equal(string.Empty, lang.DisplayName);
    }

    [Fact]
    public void Icon_Get_Returns_Value()
    {
        MLanguage lang = new()
        {
            Icon = "icon.png"
        };
        Assert.Equal("icon.png", lang.Icon);
        lang = new MLanguage();
        Assert.Equal(string.Empty, lang.Icon);
    }

    [Fact]
    public void IsDisabled_Get_Returns_Value()
    {
        MLanguage lang = new()
        {
            IsDisabled = true
        };
        Assert.True(lang.IsDisabled);
        lang = new MLanguage();
        Assert.False(lang.IsDisabled);
    }

    [Fact]
    public void ToLanguageInfo_Returns_Correct_Info()
    {
        MLanguage lang = new("en", "English", "icon", true);
        MLanguageInfo info = lang.ToLanguageInfo();

        Assert.Equal(lang.Name, info.Name);
        Assert.Equal(lang.DisplayName, info.DisplayName);
        Assert.Equal(lang.Icon, info.Icon);
        Assert.True(info.IsDisabled);
    }

    [Fact]
    public void Constructor_Default_Initializes_Properties()
    {
        MLanguage lang = new();
        Assert.Equal(string.Empty, lang.Name);
        Assert.Equal(string.Empty, lang.DisplayName);
        Assert.Equal(string.Empty, lang.Icon);
        Assert.False(lang.IsDisabled);
    }

    [Fact]
    public void Constructor_Allows_Null_Values()
    {
        MLanguage lang = new(null!, null!, null, true);
        Assert.Null(lang.Name);
        Assert.Null(lang.DisplayName);
        Assert.Null(lang.Icon);
        Assert.True(lang.IsDisabled);
    }
}
