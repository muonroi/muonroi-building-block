namespace Muonroi.Core.Abstractions.Tests;

public class RedisConfigsTests
{
    [Fact]
    public void SectionName_Getter_Returns_Value_Or_Default()
    {
        RedisConfigs configs = new();

        Assert.Equal(RedisConfigs.DefaultSectionName, configs.SectionName);

        configs.SectionName = "other";
        Assert.Equal("other", configs.SectionName);

        configs.SectionName = null!;
        Assert.Null(configs.SectionName);
    }

    [Fact]
    public void Host_Getter_Returns_Value_Or_Null()
    {
        RedisConfigs configs = new()
        {
            Host = "localhost"
        };

        Assert.Equal("localhost", configs.Host);

        configs.Host = null!;
        Assert.Null(configs.Host);
    }

    [Fact]
    public void Port_Getter_Returns_Value_Or_Null()
    {
        RedisConfigs configs = new()
        {
            Port = "6379"
        };

        Assert.Equal("6379", configs.Port);

        configs.Port = null!;
        Assert.Null(configs.Port);
    }

    [Fact]
    public void InstanceName_Getter_Returns_Value_Or_Null()
    {
        RedisConfigs configs = new()
        {
            InstanceName = "inst"
        };

        Assert.Equal("inst", configs.InstanceName);

        configs.InstanceName = null!;
        Assert.Null(configs.InstanceName);
    }

    [Fact]
    public void ClientName_Getter_Returns_Value_Or_Null()
    {
        RedisConfigs configs = new()
        {
            ClientName = "client"
        };

        Assert.Equal("client", configs.ClientName);

        configs.ClientName = null!;
        Assert.Null(configs.ClientName);
    }

    [Fact]
    public void Password_Getter_Returns_Value_Or_Null()
    {
        RedisConfigs configs = new()
        {
            Password = "pass"
        };

        Assert.Equal("pass", configs.Password);

        configs.Password = null!;
        Assert.Null(configs.Password);
    }

    [Fact]
    public void AllowAdmin_Defaults_To_False()
    {
        Assert.False(new RedisConfigs().AllowAdmin);
    }

    [Fact]
    public void Enable_Defaults_To_False()
    {
        Assert.False(new RedisConfigs().Enable);
    }

    [Fact]
    public void AllMethodsEnableCache_Defaults_To_False()
    {
        Assert.False(new RedisConfigs().AllMethodsEnableCache);
    }
}
