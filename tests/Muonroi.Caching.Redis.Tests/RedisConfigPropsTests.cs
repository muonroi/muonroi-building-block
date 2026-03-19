namespace Muonroi.Caching.Redis.Tests;

public class RedisConfigPropsTests
{
    [Fact]
    public void KeyPrefix_Default_Empty()
    {
        RedisConfigs configs = new();
        Assert.Equal(string.Empty, configs.KeyPrefix);
    }

    [Fact]
    public void Expire_Default_Zero()
    {
        RedisConfigs configs = new();
        Assert.Equal(0, configs.Expire);
    }

    [Fact]
    public void AbortOnConnectFail_Default_False()
    {
        RedisConfigs configs = new();
        Assert.False(configs.AbortOnConnectFail);
    }
}
