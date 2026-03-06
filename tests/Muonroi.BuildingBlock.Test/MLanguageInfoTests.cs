


namespace Muonroi.BuildingBlock.Test
{
    public class MLanguageInfoTests
    {
        [Fact]
        public void IsDefault_Returns_Correct_Value()
        {
            MLanguageInfo defaultLang = new("en-US", "English", null, true);
            Assert.True(defaultLang.IsDefault);

            MLanguageInfo notDefault = new("vi-VN", "Vietnamese");
            Assert.False(notDefault.IsDefault);
        }

        [Fact]
        public void IsDisabled_Returns_Correct_Value()
        {
            MLanguageInfo disabled = new("en-US", "English", null, false, true);
            Assert.True(disabled.IsDisabled);

            MLanguageInfo active = new("vi-VN", "Vietnamese");
            Assert.False(active.IsDisabled);
        }

        [Fact]
        public void IsRightToLeft_Returns_Correct_Value()
        {
            MLanguageInfo rtl = new("ar-SA", "Arabic");
            Assert.True(rtl.IsRightToLeft);

            MLanguageInfo ltr = new("en-US", "English");
            Assert.False(ltr.IsRightToLeft);
        }

        [Fact]
        public void Constructor_Initializes_Properties()
        {
            MLanguageInfo info = new("en-US", "English", "icon", true, true);

            Assert.Equal("en-US", info.Name);
            Assert.Equal("English", info.DisplayName);
            Assert.Equal("icon", info.Icon);
            Assert.True(info.IsDefault);
            Assert.True(info.IsDisabled);
        }

        [Fact]
        public void Constructor_Allows_Null_Values()
        {
            MLanguageInfo info = new(null!, null!);

            Assert.Null(info.Name);
            Assert.Null(info.DisplayName);
            Assert.Null(info.Icon);
            Assert.False(info.IsDefault);
            Assert.False(info.IsDisabled);
        }

        [Fact]
        public void Name_Property_Returns_Value()
        {
            MLanguageInfo info = new("en", "English");
            Assert.Equal("en", info.Name);
            info.Name = null!;
            Assert.Null(info.Name);
        }

        [Fact]
        public void DisplayName_Property_Returns_Value()
        {
            MLanguageInfo info = new("en", "English");
            Assert.Equal("English", info.DisplayName);
            info.DisplayName = null!;
            Assert.Null(info.DisplayName);
        }

        [Fact]
        public void Icon_Property_Returns_Value()
        {
            MLanguageInfo info = new("en", "English", "icon.png");
            Assert.Equal("icon.png", info.Icon);
            info.Icon = null;
            Assert.Null(info.Icon);
        }
    }
}
