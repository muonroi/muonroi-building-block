namespace Muonroi.AuthZ.Tests;

public class AuthZRuleEngineTests
{
    [Fact]
    public void AuthorizationRuleContext_HaltGroup_SetsIsHalted()
    {
        AuthorizationRuleContext context = new();

        context.HaltGroup();

        Assert.True(context.IsHalted);
    }

    [Fact]
    public void RowFilterContext_HaltGroup_SetsIsHalted()
    {
        RowFilterContext<string> context = new();

        context.HaltGroup();

        Assert.True(context.IsHalted);
    }

    [Fact]
    public void AddMAuthorizationHotReload_RegistersOptionsAndHostedService()
    {
        ServiceCollection services = [];
        services.AddSingleton<IAuthRuleChangeHandler, RecordingAuthRuleChangeHandler>();
        services.AddSingleton<ITenantContext, FakeTenantContext>();

        services.AddMAuthorizationHotReload(options =>
        {
            options.ControlPlaneUrl = "https://control-plane.example";
            options.TenantId = "tenant-a";
        });

        ServiceProvider provider = services.BuildServiceProvider();
        AuthRuleHotReloadOptions options = provider.GetRequiredService<AuthRuleHotReloadOptions>();
        IEnumerable<IHostedService> hostedServices = provider.GetServices<IHostedService>();

        Assert.Equal("https://control-plane.example", options.ControlPlaneUrl);
        Assert.Equal("tenant-a", options.TenantId);
        Assert.Contains(hostedServices, service => service is AuthRuleHotReloadClient);
    }

    [Fact]
    public async Task AuthRuleHotReloadClient_WithoutControlPlaneUrl_CompletesWithoutInvokingHandler()
    {
        RecordingAuthRuleChangeHandler handler = new();
        AuthRuleHotReloadClient client = new(new AuthRuleHotReloadOptions(), handler, new FakeTenantContext());

        await client.StartAsync(CancellationToken.None);
        await client.StopAsync(CancellationToken.None);

        Assert.Empty(handler.RuleSetIds);
    }

    private sealed class RecordingAuthRuleChangeHandler : IAuthRuleChangeHandler
    {
        public List<Guid> RuleSetIds { get; } = [];

        public Task OnAuthRuleChangedAsync(Guid ruleSetId, CancellationToken ct = default)
        {
            RuleSetIds.Add(ruleSetId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public string? TenantId { get; set; }
        public string? Language { get; set; }
        public bool AllowCrossTenantAccess { get; set; }
    }
}
