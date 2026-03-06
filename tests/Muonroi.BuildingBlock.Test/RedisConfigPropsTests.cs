namespace Muonroi.BuildingBlock.Test;

public class RedisConfigPropsTests
{
    [Fact]
    public void KeyPrefix_Default_Empty()
    {
        RedisConfigs cfg = new();
        Assert.Equal(string.Empty, cfg.KeyPrefix);
    }

    [Fact]
    public void Expire_Default_Zero()
    {
        RedisConfigs cfg = new();
        Assert.Equal(0, cfg.Expire);
    }

    [Fact]
    public void AbortOnConnectFail_Default_False()
    {
        RedisConfigs cfg = new();
        Assert.False(cfg.AbortOnConnectFail);
    }
}
