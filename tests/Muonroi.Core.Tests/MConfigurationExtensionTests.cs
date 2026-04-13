using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.Core.Tests;

public class MConfigurationExtensionTests
{
    [Fact]
    public void GetConfigHelper_Returns_Plain_Value_When_Encryption_Disabled()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "false",
                ["MyKey"] = "value"
            })
            .Build();

        configuration.GetConfigHelper("MyKey").Should().Be("value");
    }

    [Fact]
    public void GetConfigHelper_Returns_Empty_When_Key_Missing()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "false"
            })
            .Build();

        configuration.GetConfigHelper("Missing").Should().BeEmpty();
    }

    [Fact]
    public void GetCryptConfigValue_Throws_When_Key_Is_Empty()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        Action action = () => configuration.GetCryptConfigValue(string.Empty);

        action.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void ConfigureDictionary_Binds_KeyValue_Pairs()
    {
        ServiceCollection services = [];
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Test:One"] = "1",
                ["Test:Two"] = "2"
            })
            .Build();

        services.ConfigureDictionary<Dictionary<string, string>>(configuration.GetSection("Test"));
        using ServiceProvider provider = services.BuildServiceProvider();

        Dictionary<string, string> options =
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Dictionary<string, string>>>().Value;

        options.Should().ContainKey("One").WhoseValue.Should().Be("1");
        options.Should().ContainKey("Two").WhoseValue.Should().Be("2");
    }
}
