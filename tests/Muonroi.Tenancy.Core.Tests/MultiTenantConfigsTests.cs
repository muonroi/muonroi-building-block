namespace Muonroi.Tenancy.Core.Tests;

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
        MultiTenantConfigs configs = new();

        Assert.True(configs.Enabled);
    }

    [Fact]
    public void RequireTenantClaimForAuthenticatedUser_Defaults_To_True()
    {
        MultiTenantConfigs configs = new();

        Assert.True(configs.RequireTenantClaimForAuthenticatedUser);
    }
}
