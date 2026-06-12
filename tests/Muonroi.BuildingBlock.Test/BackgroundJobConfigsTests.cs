

namespace Muonroi.BuildingBlock.Test;

public class BackgroundJobConfigsTests
{
    [Fact]
    public void JobType_Default_Is_Hangfire()
    {
        BackgroundJobConfigs cfg = new();
        Assert.Equal(JobType.Hangfire, cfg.JobType);
    }

    [Fact]
    public void ConnectionString_Get_Returns_Set_Value()
    {
        BackgroundJobConfigs cfg = new();
        Assert.Null(cfg.ConnectionString);
        cfg.ConnectionString = "conn";
        Assert.Equal("conn", cfg.ConnectionString);
    }
}
