namespace Muonroi.Core.Abstractions.Tests;

public class MHelpersTests
{
    [Fact]
    public void Initialize_Allows_Reading_Configured_Setting()
    {
        ResourceSetting settings = new()
        {
            [nameof(SystemSettingKey.ResourceName)] = "Resources.ErrorMessages",
            [ResourceSettingKeys.Lang] = "en-US"
        };

        MHelpers.Initialize(settings, typeof(MHelpers).Assembly);

        nameof(SystemSettingKey.ResourceName).GetSettingValue().Should().Be("Resources.ErrorMessages");
    }

    [Fact]
    public void GetSettingValue_Returns_Default_When_Key_Missing()
    {
        MHelpers.Initialize(new ResourceSetting(), typeof(MHelpers).Assembly);

        "Unknown".GetSettingValue().Should().Be("vi-VN");
    }

    [Fact]
    public void GenerateErrorResult_Formats_LowerCamel_Property_Name()
    {
        MHelpers.GenerateErrorResult("Name", 1).Should().Be("name: 1");
    }

    [Fact]
    public void GetErrorMessage_Returns_Default_When_Assembly_Has_No_Resources()
    {
        MHelpers.Initialize(new ResourceSetting
        {
            [ResourceSettingKeys.Lang] = "en-US"
        }, typeof(MHelpers).Assembly);

        MHelpers.GetErrorMessage("UNKNOWN").Should().Be("No pre-defined error message");
    }

    [Fact]
    public void LoadErrorMessages_Returns_Empty_When_Resource_Not_Found()
    {
        MethodInfo method = typeof(MHelpers).GetMethod("LoadErrorMessages", BindingFlags.NonPublic | BindingFlags.Static)!;

        Dictionary<string, string> result =
            (Dictionary<string, string>)method.Invoke(null, [typeof(string).Assembly, "en-US"])!;

        result.Should().BeEmpty();
    }
}
