namespace Muonroi.BuildingBlock.Test;

public class MultiTenantConfigsTests
{
    [Fact]
    public void Section_Returns_SectionName()
    {
        Assert.Equal(MultiTenantConfigs.SectionName, MultiTenantConfigs.Section);
    }

    [Fact]
    public void Enabled_Defaults_To_True()
    {
        MultiTenantConfigs cfg = new();
        Assert.True(cfg.Enabled);
    }

    [Fact]
    public void RequireTenantClaimForAuthenticatedUser_Defaults_To_True()
    {
        MultiTenantConfigs cfg = new();
        Assert.True(cfg.RequireTenantClaimForAuthenticatedUser);
    }
}
