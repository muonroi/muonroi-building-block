namespace Muonroi.BuildingBlock.Test;

public class ConsulConfigsTests
{
    [Fact]
    public void Id_Getter_Returns_Value_Or_Null()
    {
        ConsulConfigs cfg = new()
        {
            Id = "id"
        };
        Assert.Equal("id", cfg.Id);
        cfg.Id = null!;
        Assert.Null(cfg.Id);
    }

    [Fact]
    public void ServiceName_Getter_Returns_Value_Or_Null()
    {
        ConsulConfigs cfg = new()
        {
            ServiceName = "svc"
        };
        Assert.Equal("svc", cfg.ServiceName);
        cfg.ServiceName = null!;
        Assert.Null(cfg.ServiceName);
    }

    [Fact]
    public void ConsulAddress_Getter_Returns_Value_Or_Null()
    {
        ConsulConfigs cfg = new()
        {
            ConsulAddress = "addr"
        };
        Assert.Equal("addr", cfg.ConsulAddress);
        cfg.ConsulAddress = null!;
        Assert.Null(cfg.ConsulAddress);
    }

    [Fact]
    public void ServiceAddress_Getter_Returns_Value_Or_Null()
    {
        ConsulConfigs cfg = new()
        {
            ServiceAddress = "saddr"
        };
        Assert.Equal("saddr", cfg.ServiceAddress);
        cfg.ServiceAddress = null!;
        Assert.Null(cfg.ServiceAddress);
    }

    [Fact]
    public void ServicePort_Getter_Returns_Value_Or_Default()
    {
        ConsulConfigs cfg = new()
        {
            ServicePort = 123
        };
        Assert.Equal(123, cfg.ServicePort);
        cfg.ServicePort = 0;
        Assert.Equal(0, cfg.ServicePort);
    }

    [Fact]
    public void ServiceMetadata_Getter_Returns_Value_Or_Null()
    {
        ConsulConfigs cfg = new()
        {
            ServiceMetadata = new Dictionary<string, string>
            {
                ["a"] = "b"
            }
        };
        Assert.NotNull(cfg.ServiceMetadata);
        Assert.Equal("b", cfg.ServiceMetadata!["a"]);
        cfg.ServiceMetadata = null!;
        Assert.Null(cfg.ServiceMetadata);
    }
}
