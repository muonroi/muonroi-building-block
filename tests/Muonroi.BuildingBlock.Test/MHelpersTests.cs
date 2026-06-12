using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class MHelpersTests
{
    private static ResourceSetting CreateSetting()
    {
        ResourceSetting setting = new()
        {
            [nameof(SystemSettingKey.ResourceName)] = "Resources.ErrorMessages",
            [ResourceSettingKeys.Lang] = "en-US"
        };
        return setting;
    }

    private static void Init()
    {
        MHelpers.Initialize(CreateSetting(), new MJsonSerializeService(), typeof(MHelpers).Assembly);
    }

    [Fact]
    public void Initialize_Sets_Settings()
    {
        Init();
        string value = nameof(SystemSettingKey.ResourceName).GetSettingValue();
        Assert.Equal("Resources.ErrorMessages", value);
    }

    [Fact]
    public void Initialize_Allows_Null_Values()
    {
        MHelpers.Initialize(null!, null!, null!);
        string value = "any".GetSettingValue();
        Assert.Equal("vi-VN", value);
    }

    [Fact]
    public void GetConfigHelper_Returns_Config_Value()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EnableEncryption"] = "false",
            ["MyKey"] = "value"
        }).Build();

        string result = config.GetConfigHelper("MyKey");
        Assert.Equal("value", result);
    }

    [Fact]
    public void GetConfigHelper_Missing_SecretKey_Throws()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EnableEncryption"] = "true"
        }).Build();

        Assert.Throws<MInternalException>(() => config.GetConfigHelper("MyKey"));
    }

    [Fact]
    public void GetConfigHelper_Missing_Value_Returns_Empty()
    {
        Dictionary<string, string?> data = new()
            { { "SecretKey", "s" }, { "EnableEncryption", "false" } };
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        string val = cfg.GetConfigHelper("Missing");
        Assert.Equal(string.Empty, val);
    }

    [Fact]
    public void GenerateErrorResult_Returns_Message()
    {
        string result = MHelpers.GenerateErrorResult("Name", 1);
        Assert.Equal("name: 1", result);
    }

    [Fact]
    public void GenerateErrorResult_Null_Name_Throws()
    {
        Assert.Throws<NullReferenceException>(() => MHelpers.GenerateErrorResult(null!, 1));
    }

    [Fact]
    public void GenerateErrorResult_Null_Value_Works()
    {
        string result = MHelpers.GenerateErrorResult("Name", null!);
        Assert.Equal("name: ", result);
    }

    [Fact]
    public void LoadErrorMessages_Returns_Dictionary()
    {
        Init();
        MethodInfo mi = typeof(MHelpers).GetMethod("LoadErrorMessages", BindingFlags.NonPublic | BindingFlags.Static)!;
        Dictionary<string, string> dict = (Dictionary<string, string>)mi.Invoke(null, [typeof(MHelpers).Assembly, "en-US"])!;
        Assert.NotEmpty(dict);
        Assert.Contains("ERROR", dict.Keys);
    }

    [Fact]
    public void LoadErrorMessages_File_Not_Found_Returns_Empty()
    {
        Init();
        MethodInfo mi = typeof(MHelpers).GetMethod("LoadErrorMessages", BindingFlags.NonPublic | BindingFlags.Static)!;
        Dictionary<string, string> dict = (Dictionary<string, string>)mi.Invoke(null, [typeof(string).Assembly, "en-US"])!;
        Assert.Empty(dict);
    }

    [Fact]
    public void SetErrorMessagesOfLanguage_Adds_Language()
    {
        Init();
        MethodInfo miSet = typeof(MHelpers).GetMethod("SetErrorMessagesOfLanguage",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        string fileKey = $"{typeof(MHelpers).Assembly.GetName().Name}@@fr-FR";
        Dictionary<string, string> dict = new()
        {
            ["E"] = "msg"
        };
        miSet.Invoke(null, [fileKey, dict]);
        string msg = MHelpers.GetErrorMessage("E", "fr-FR");
        Assert.Equal("msg", msg);
    }

    [Fact]
    public void GetFromResources_Returns_Content()
    {
        Init();
        MethodInfo mi = typeof(MHelpers).GetMethod("GetFromResources", BindingFlags.NonPublic | BindingFlags.Static)!;
        string content = (string)mi.Invoke(null, ["Resources.ErrorMessages-en-US.json", typeof(MHelpers).Assembly])!;
        Assert.Contains("ERROR", content);
    }

    [Fact]
    public void GetFromResources_NotFound_Returns_Empty()
    {
        Init();
        MethodInfo mi = typeof(MHelpers).GetMethod("GetFromResources", BindingFlags.NonPublic | BindingFlags.Static)!;
        string content = (string)mi.Invoke(null, ["NotExist.json", typeof(MHelpers).Assembly])!;
        Assert.Equal(string.Empty, content);
    }

    [Fact]
    public void GetSettingValue_Returns_From_Setting()
    {
        Init();
        string value = "ResourceName".GetSettingValue();
        Assert.Equal("Resources.ErrorMessages", value);
    }

    [Fact]
    public void GetSettingValue_Missing_Key_Returns_Default()
    {
        Init();
        string value = "Unknown".GetSettingValue();
        Assert.Equal("vi-VN", value);
    }

    [Fact]
    public void GetSettingValue_Null_Setting_Returns_Default()
    {
        MHelpers.Initialize(null!, new MJsonSerializeService(), typeof(MHelpers).Assembly);
        string value = "ResourceName".GetSettingValue();
        Assert.Equal("vi-VN", value);
    }

    [Fact]
    public void MethodResultHelpers_GetErrorMessage_Returns_Message()
    {
        Init();
        string msg = MMethodResultHelpers.GetErrorMessage("ERROR");
        Assert.Equal("An error occurred.", msg);
    }

    [Fact]
    public void MethodResultHelpers_GetErrorMessage_WithLang_Returns_Message()
    {
        Init();
        string msg = MMethodResultHelpers.GetErrorMessage("ERROR", "vi-VN");
        Assert.Equal("Đã xảy ra lỗi.", msg);
    }

    [Fact]
    public void MethodResultHelpers_GetErrorMessage_Missing_Key_Returns_Default()
    {
        Init();
        string msg = MMethodResultHelpers.GetErrorMessage("UNKNOWN");
        Assert.Equal("No pre-defined error message", msg);
    }
}
