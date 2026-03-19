namespace Muonroi.Caching.Abstractions.Tests;

public class DistributedCacheKeyBuilderTests
{
    [Fact]
    public void Build_Uses_Namespace_And_Tenant()
    {
        string key = DistributedCacheKeyBuilder.Build("k", "svc-a", "tenant-1");

        key.Should().Be("svc-a:tenant-1:k");
    }

    [Fact]
    public void Build_Falls_Back_To_Current_TenantContext()
    {
        string? original = TenantContext.CurrentTenantId;

        try
        {
            TenantContext.CurrentTenantId = "tenant-a";

            DistributedCacheKeyBuilder.Build("shared-key").Should().Be("tenant-a:shared-key");
        }
        finally
        {
            TenantContext.CurrentTenantId = original;
        }
    }

    [Fact]
    public void NormalizeTenantId_Trims_Whitespace_And_Nulls_Empty()
    {
        DistributedCacheKeyBuilder.NormalizeTenantId(" tenant-a ").Should().Be("tenant-a");
        DistributedCacheKeyBuilder.NormalizeTenantId(" ").Should().BeNull();
    }

    [Fact]
    public void Build_Handles_Null_Arguments()
    {
        DistributedCacheKeyBuilder.Build("key", null, null).Should().Be("key");
    }
}
