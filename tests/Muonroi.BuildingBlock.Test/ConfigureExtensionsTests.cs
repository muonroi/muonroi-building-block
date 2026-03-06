namespace Muonroi.BuildingBlock.Test;

public class ConfigureExtensionsTests
{
    [Fact]
    public void ConfigureDictionary_Adds_KeyValue_Pairs()
    {
        Dictionary<string, string?> data = new()
            { { "Section:One", "1" }, { "Section:Two", "2" } };
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        IServiceCollection services = new ServiceCollection();

        services.ConfigureDictionary<Dictionary<string, string>>(cfg.GetSection("Section"));
        ServiceProvider provider = services.BuildServiceProvider();
        Dictionary<string, string> dict = provider.GetRequiredService<IOptions<Dictionary<string, string>>>().Value;

        Assert.Equal("1", dict["One"]);
        Assert.Equal("2", dict["Two"]);
    }

    [Fact]
    public void ConfigureDictionary_Empty_Section_Yields_Empty_Dictionary()
    {
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        IServiceCollection services = new ServiceCollection();
        services.ConfigureDictionary<Dictionary<string, string>>(cfg.GetSection("None"));
        Dictionary<string, string> dict = services.BuildServiceProvider().GetRequiredService<IOptions<Dictionary<string, string>>>().Value;
        Assert.Empty(dict);
    }

    [Fact]
    public void ConfigureDictionary_Duplicate_Key_Throws()
    {
        IEnumerable<KeyValuePair<string, string?>> data =
        [
            new("S:Key", "1"),
            new("S:Key", "2")
        ];
        Assert.Throws<ArgumentException>(() =>
        {
            IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            ServiceCollection services = [];
            services.ConfigureDictionary<Dictionary<string, string>>(cfg.GetSection("S"));
        });
    }

    private class StartConfig
    {
        public string Value { get; set; } = string.Empty;
        public int Number { get; set; } = 0;
    }

    [Fact]
    public void ConfigureStartupConfig_Binds_Config()
    {
        Dictionary<string, string?> data = new()
            { { "Value", "abc" }, { "Number", "5" } };
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        IServiceCollection services = new ServiceCollection();
        StartConfig config = services.ConfigureStartupConfig<StartConfig>(cfg);
        Assert.Equal("abc", config.Value);
        Assert.Equal(5, config.Number);
    }

    [Fact]
    public void ConfigureStartupConfig_Null_Config_Throws()
    {
        IServiceCollection services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.ConfigureStartupConfig<StartConfig>(null!));
    }

    [Fact]
    public void ConfigureStartupConfig_Invalid_Format_Uses_Default()
    {
        Dictionary<string, string?> data = new()
            { { "Number", "bad" } };
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        IServiceCollection services = new ServiceCollection();
        StartConfig config = services.ConfigureStartupConfig<StartConfig>(cfg);
        Assert.Equal(0, config.Number);
    }
}
