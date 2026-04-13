namespace Muonroi.BuildingBlock.Test;

public class TenantContextTests
{
    [Fact]
    public async Task TenantContext_Is_Isolated_Per_AsyncFlow()
    {
        TenantContext.CurrentTenantId = "parent";
        string? taskTenant = null;
        await Task.Run(() =>
        {
            TenantContext.CurrentTenantId = "child";
            taskTenant = TenantContext.CurrentTenantId;
        });

        Assert.Equal("parent", TenantContext.CurrentTenantId);
        Assert.Equal("child", taskTenant);
    }

    [Fact]
    public async Task Middleware_Sets_Tenant_From_Header()
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, "headerTenant");

        bool nextCalled = false;

        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            Assert.Equal("headerTenant", TenantContext.CurrentTenantId);
            return Task.CompletedTask;
        }

        TenantContextMiddleware middleware = new(Next, new DefaultTenantIdResolver());
        await middleware.Invoke(context);
        Assert.True(nextCalled);
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public void TenantId_Defaults_To_Null()
    {
        TenantContext context = new();
        Assert.Null(context.TenantId);
    }

    [Fact]
    public void TenantId_Set_And_Clear_Works()
    {
        TenantContext context = new()
        {
            TenantId = "t1"
        };
        Assert.Equal("t1", context.TenantId);
        context.TenantId = null;
        Assert.Null(context.TenantId);
    }
}
