namespace Muonroi.Caching.Memory.Tests;

public class CacheConfigsTests
{
    [Fact]
    public void CacheType_Default_Is_Memory()
    {
        CacheConfigs configs = new();
        Assert.Equal(MultiLevelCacheType.Memory, configs.CacheType);
    }

    [Fact]
    public void CacheType_Returns_Set_Value()
    {
        CacheConfigs configs = new()
        {
            CacheType = MultiLevelCacheType.Redis
        };
        Assert.Equal(MultiLevelCacheType.Redis, configs.CacheType);
    }

    [Fact]
    public void SectionName_Default_Returns()
    {
        CacheConfigs configs = new();
        Assert.Equal(CacheConfigs.DefaultSectionName, configs.SectionName);
    }

    [Fact]
    public void StampedeProtection_Default_Is_Enabled()
    {
        CacheConfigs configs = new();
        Assert.True(configs.EnableStampedeProtection);
    }

    [Fact]
    public void DefaultExpiration_Default_Is_1440()
    {
        CacheConfigs configs = new();
        Assert.Equal(1440, configs.DefaultAbsoluteExpirationInMinutes);
    }
}
