namespace Muonroi.AspNetCore.Tests.Controllers;

public class TenantQuotaControllerTests
{
    private readonly ITenantQuotaTracker _quotaTracker;
    private readonly ITenantQuotaStore _quotaStore;
    private readonly TenantQuotaController _controller;

    public TenantQuotaControllerTests()
    {
        _quotaTracker = Substitute.For<ITenantQuotaTracker>();
        _quotaStore = Substitute.For<ITenantQuotaStore>();
        _controller = new TenantQuotaController(_quotaTracker, _quotaStore);
    }

    [Fact]
    public async Task GetUsage_Forbidden_ReturnsForbid()
    {
        TenantContext.CurrentTenantId = "tenant1";
        var result = await _controller.GetUsage("tenant2", CancellationToken.None);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetUsage_Allowed_ReturnsOk()
    {
        TenantContext.CurrentTenantId = "tenant1";
        var usage = new QuotaUsage();
        _quotaTracker.GetUsageAsync("tenant1", Arg.Any<CancellationToken>()).Returns(usage);

        var result = await _controller.GetUsage("tenant1", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(usage, okResult.Value);
    }

    [Fact]
    public async Task GetLimits_Allowed_ReturnsOk()
    {
        TenantContext.CurrentTenantId = "tenant1";
        var quota = new TenantQuota { TenantId = "tenant1" };
        _quotaStore.GetQuotaAsync("tenant1", Arg.Any<CancellationToken>()).Returns(quota);

        var result = await _controller.GetLimits("tenant1", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(quota, okResult.Value);
    }

    [Fact]
    public async Task UpdateLimits_ReturnsOk()
    {
        var quota = new TenantQuota();
        var result = await _controller.UpdateLimits("tenant1", quota, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await _quotaStore.Received(1).SaveQuotaAsync("tenant1", quota, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpgradeTier_Allowed_ReturnsOk()
    {
        TenantContext.CurrentTenantId = "tenant1";
        var request = new UpgradeRequest(TenantTier.Enterprise);

        var result = await _controller.UpgradeTier("tenant1", request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await _quotaStore.Received(1).SaveQuotaAsync("tenant1", Arg.Is<TenantQuota>(q => q.Tier == TenantTier.Enterprise), Arg.Any<CancellationToken>());
    }
}
