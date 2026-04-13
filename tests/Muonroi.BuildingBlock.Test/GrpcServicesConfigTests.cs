
namespace Muonroi.BuildingBlock.Test;

public class GrpcServicesConfigTests
{
    [Fact]
    public void Services_Property_Returns_Set_Value()
    {
        GrpcServiceConfig svc = new()
        {
            Uri = "http://a"
        };
        Dictionary<string, GrpcServiceConfig> dict = new()
        {
            ["a"] = svc
        };
        GrpcServicesConfig cfg = new()
        {
            Services = dict
        };
        Assert.Same(dict, cfg.Services);
    }

    [Fact]
    public void Services_Property_Null_Or_Empty()
    {
        GrpcServicesConfig cfg = new();
        Assert.NotNull(cfg.Services);
        Assert.Empty(cfg.Services);
        cfg.Services = null!;
        Assert.Null(cfg.Services);
    }

    [Fact]
    public void Uri_Property_Returns_Set_Value()
    {
        GrpcServiceConfig cfg = new()
        {
            Uri = "http://x"
        };
        Assert.Equal("http://x", cfg.Uri);
    }

    [Fact]
    public void Uri_Property_Defaults_To_Empty()
    {
        GrpcServiceConfig cfg = new();
        Assert.Equal(string.Empty, cfg.Uri);
        cfg.Uri = null!;
        Assert.Null(cfg.Uri);
    }
}
