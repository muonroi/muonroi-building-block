namespace Muonroi.BuildingBlock.Test;

public class MConnectionStringProviderTests
{
    [Fact]
    public void GetConnectionString_Returns_Value_From_Config()
    {
        Dictionary<string, string?> data = new()
        {
            ["EnableEncryption"] = "false",
            ["Default:ConnectionString"] = "DataSource=db;"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        MConnectionStringProvider provider = new(config, new ConfigurationSecretProvider(config));

        string result = provider.GetConnectionString("Default");

        Assert.Equal("DataSource=db;", result);
    }

    [Fact]
    public void GetConnectionString_Key_NotFound_Returns_Empty()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["EnableEncryption"] = "false" }).Build();
        MConnectionStringProvider provider = new(config, new ConfigurationSecretProvider(config));

        string result = provider.GetConnectionString("Missing");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetConnectionString_MissingSecretKey_Throws()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["EnableEncryption"] = "true" }).Build();
        MConnectionStringProvider provider = new(config, new ConfigurationSecretProvider(config));

        Assert.Throws<InvalidOperationException>(() => provider.GetConnectionString("Default"));
    }
}
