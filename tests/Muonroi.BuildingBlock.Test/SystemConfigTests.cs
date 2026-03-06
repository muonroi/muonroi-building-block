namespace Muonroi.BuildingBlock.Test;

public class SystemConfigTests
{
    [Fact]
    public void AddSystemConfig_Registers_ResourceSetting()
    {
        ServiceCollection services = [];
        Dictionary<string, string?> data = new()
        {
            ["ResourceSetting:Key"] = "val"
        };
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection(data).Build();

        services.AddSystemConfig(cfg);
        ServiceProvider provider = services.BuildServiceProvider();
        ResourceSetting? setting = provider.GetService<ResourceSetting>();

        Assert.NotNull(setting);
        Assert.Equal("val", setting!["Key"]);
    }

    [Fact]
    public void AddSystemConfig_NullConfig_Throws()
    {
        ServiceCollection services = [];
        Assert.Throws<NullReferenceException>(() => services.AddSystemConfig(null!));
    }

    [Fact]
    public void AddSystemConfig_Allows_Duplicate_Registrations()
    {
        ServiceCollection services = [];
        IConfiguration cfg = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        services.AddSystemConfig(cfg);
        services.AddSystemConfig(cfg);

        int count = services.Count(d => d.ServiceType == typeof(ResourceSetting));
        Assert.Equal(2, count);
    }
}
