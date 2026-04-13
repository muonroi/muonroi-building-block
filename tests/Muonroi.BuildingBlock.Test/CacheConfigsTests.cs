

namespace Muonroi.BuildingBlock.Test;

public class CacheConfigsTests
{
    [Fact]
    public void CacheType_Default_Is_Memory()
    {
        CacheConfigs cfg = new();
        Assert.Equal(MultiLevelCacheType.Memory, cfg.CacheType);
    }

    [Fact]
    public void CacheType_Returns_Set_Value()
    {
        CacheConfigs cfg = new()
        {
            CacheType = MultiLevelCacheType.Redis
        };
        Assert.Equal(MultiLevelCacheType.Redis, cfg.CacheType);
    }

    [Fact]
    public void SectionName_Default_Returns()
    {
        CacheConfigs cfg = new();
        Assert.Equal(CacheConfigs.DefaultSectionName, cfg.SectionName);
    }

    [Fact]
    public void StampedeProtection_Default_Is_Enabled()
    {
        CacheConfigs cfg = new();
        Assert.True(cfg.EnableStampedeProtection);
    }

    [Fact]
    public void DefaultExpiration_Default_Is_1440()
    {
        CacheConfigs cfg = new();
        Assert.Equal(1440, cfg.DefaultAbsoluteExpirationInMinutes);
    }
}
