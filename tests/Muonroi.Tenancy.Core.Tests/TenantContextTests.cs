namespace Muonroi.Tenancy.Core.Tests;

public class TenantContextTests
{
    [Fact]
    public void Instance_TenantId_Reads_And_Writes_AsyncLocal()
    {
        TenantContext context = new();
        context.TenantId = "test-tenant";

        context.TenantId.Should().Be("test-tenant");
    }

    [Fact]
    public void Static_CurrentTenantId_Reads_And_Writes_AsyncLocal()
    {
        TenantContext.CurrentTenantId = "static-tenant";

        TenantContext.CurrentTenantId.Should().Be("static-tenant");
    }

    [Fact]
    public void Instance_And_Static_Share_Same_AsyncLocal()
    {
        TenantContext context = new();
        context.TenantId = "shared-tenant";

        TenantContext.CurrentTenantId.Should().Be("shared-tenant");
    }

    [Fact]
    public void TenantId_Is_Null_By_Default_In_New_Context()
    {
        // Reset to null first to avoid test pollution
        TenantContext.CurrentTenantId = null;

        TenantContext context = new();
        context.TenantId.Should().BeNull();
    }

    [Fact]
    public async Task AsyncLocal_Is_Isolated_Across_Tasks()
    {
        TenantContext.CurrentTenantId = "parent-tenant";

        string? childValue = await Task.Run(() =>
        {
            // Child task inherits parent value but changes don't flow back
            TenantContext.CurrentTenantId = "child-tenant";
            return TenantContext.CurrentTenantId;
        });

        childValue.Should().Be("child-tenant");
        TenantContext.CurrentTenantId.Should().Be("parent-tenant");
    }
}
