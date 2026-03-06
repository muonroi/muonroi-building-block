namespace Muonroi.BuildingBlock.Test;

public class RedisConfigsTests
{
    [Fact]
    public void SectionName_Getter_Returns_Value_Or_Default()
    {
        RedisConfigs cfg = new();
        Assert.Equal(RedisConfigs.DefaultSectionName, cfg.SectionName);
        cfg.SectionName = "other";
        Assert.Equal("other", cfg.SectionName);
        cfg.SectionName = null!;
        Assert.Null(cfg.SectionName);
    }

    [Fact]
    public void Host_Getter_Returns_Value_Or_Null()
    {
        RedisConfigs cfg = new()
        {
            Host = "localhost"
        };
        Assert.Equal("localhost", cfg.Host);
        cfg.Host = null!;
        Assert.Null(cfg.Host);
    }

    [Fact]
    public void Port_Getter_Returns_Value_Or_Null()
    {
        RedisConfigs cfg = new()
        {
            Port = "6379"
        };
        Assert.Equal("6379", cfg.Port);
        cfg.Port = null!;
        Assert.Null(cfg.Port);
    }

    [Fact]
    public void InstanceName_Getter_Returns_Value_Or_Null()
    {
        RedisConfigs cfg = new()
        {
            InstanceName = "inst"
        };
        Assert.Equal("inst", cfg.InstanceName);
        cfg.InstanceName = null!;
        Assert.Null(cfg.InstanceName);
    }

    [Fact]
    public void ClientName_Getter_Returns_Value_Or_Null()
    {
        RedisConfigs cfg = new()
        {
            ClientName = "client"
        };
        Assert.Equal("client", cfg.ClientName);
        cfg.ClientName = null!;
        Assert.Null(cfg.ClientName);
    }

    [Fact]
    public void Password_Getter_Returns_Value_Or_Null()
    {
        RedisConfigs cfg = new()
        {
            Password = "pass"
        };
        Assert.Equal("pass", cfg.Password);
        cfg.Password = null!;
        Assert.Null(cfg.Password);
    }

    [Fact]
    public void AllowAdmin_Defaults_To_False()
    {
        RedisConfigs cfg = new();
        Assert.False(cfg.AllowAdmin);
    }

    [Fact]
    public void Enable_Defaults_To_False()
    {
        RedisConfigs cfg = new();
        Assert.False(cfg.Enable);
    }

    [Fact]
    public void AllMethodsEnableCache_Defaults_To_False()
    {
        RedisConfigs cfg = new();
        Assert.False(cfg.AllMethodsEnableCache);
    }
}
