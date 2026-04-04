using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class MConfigurationExtensionTests
{
    private class MyOptions
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; } = 0;
    }

    [Fact]
    public void GetOptions_Binds_Correctly()
    {
        Dictionary<string, string?> data = new()
        {
            { "Test:Name", "John" },
            { "Test:Age", "30" }
        };
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        MyOptions opt = cfg.GetOptions<MyOptions>("Test");
        Assert.Equal("John", opt.Name);
        Assert.Equal(30, opt.Age);
    }

    [Fact]
    public void GetOptions_Missing_Section_Returns_Default_Object()
    {
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        MyOptions opt = cfg.GetOptions<MyOptions>("Missing");
        Assert.Equal(string.Empty, opt.Name);
        Assert.Equal(0, opt.Age);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetOptions_NullOrEmpty_Key_Throws(string? key)
    {
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        Assert.Throws<MArgumentException>(() => cfg.GetOptions<MyOptions>(key!));
    }

    [Fact]
    public void GetOptions_Invalid_Format_Uses_Default()
    {
        Dictionary<string, string?> data = new()
            { { "Test:Age", "invalid" } };
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        MyOptions opt = cfg.GetOptions<MyOptions>("Test");
        Assert.Equal(0, opt.Age);
    }

    [Fact]
    public void GetCryptConfigValue_Returns_Value()
    {
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Key", "Value" } }).Build();
        string? val = cfg.GetCryptConfigValue("Key");
        Assert.Equal("Value", val);
    }

    [Fact]
    public void GetCryptConfigValue_Key_Not_Found_Returns_Null()
    {
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        string? val = cfg.GetCryptConfigValue("Missing");
        Assert.Null(val);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetCryptConfigValue_Null_Or_Empty_Key_Throws(string? key)
    {
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        Assert.Throws<MArgumentException>(() => cfg.GetCryptConfigValue(key!));
    }

    [Fact]
    public void GetCryptConfigValue_With_SecretKey_Returns_Decrypted()
    {
        string secret = "secretkey123456789012345678901234";
        string plain = "hello";
        string cipher = MCryptographyExtension.Encrypt(secret, plain);
        Dictionary<string, string?> data = new()
            { { "Cipher", cipher }, { "EnableEncryption", "true" } };
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        string? val = cfg.GetCryptConfigValue("Cipher", secret);
        Assert.Equal(plain, val);
    }

    [Fact]
    public void GetCryptConfigValue_With_SecretKey_Key_Not_Found_Returns_Null()
    {
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        string? val = cfg.GetCryptConfigValue("Missing", "s");
        Assert.Null(val);
    }

    [Fact]
    public void GetCryptConfigValue_With_SecretKey_Invalid_Cipher_Throws()
    {
        Dictionary<string, string?> data = new()
            { { "Cipher", "invalid" }, { "EnableEncryption", "true" } };
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        string? value = cfg.GetCryptConfigValue("Cipher", "secret");
        Assert.NotEqual("invalid", value);
    }

    [Fact]
    public void GetCryptConfigValue_Configured_Secret_Key_Returns_Decrypted()
    {
        string secret = "secretkey123456789012345678901234";
        string plain = "ping";
        string cipher = MCryptographyExtension.Encrypt(secret, plain);
        Dictionary<string, string?> data = new()
        {
            { "Cipher", cipher },
            { "SecretKey", secret },
            { "EnableEncryption", "true" }
        };
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        string? val = cfg.GetCryptConfigValue("Cipher", true, string.Empty);
        Assert.Equal(plain, val);
    }

    [Fact]
    public void GetCryptConfigValue_Configured_Key_Not_Found_Returns_Null()
    {
        Dictionary<string, string?> data = new()
            { { "EnableEncryption", "true" } };
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        string? val = cfg.GetCryptConfigValue("Missing", true, string.Empty);
        Assert.Null(val);
    }
}
