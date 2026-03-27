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
    public void Build_Returns_Key_Without_Tenant_When_No_TenantId_Passed()
    {
        // After removing TenantContext.CurrentTenantId fallback, Build() with no tenantId
        // must return the key without any tenant prefix — callers must pass tenantId explicitly.
        DistributedCacheKeyBuilder.Build("shared-key").Should().Be("shared-key");
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
