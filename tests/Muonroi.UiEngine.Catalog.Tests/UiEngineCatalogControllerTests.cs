using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Muonroi.Core.Abstractions.Context;
using Muonroi.UiEngine.Catalog.Controllers;
using Muonroi.UiEngine.Catalog.Models;
using Muonroi.UiEngine.Catalog.Services;

namespace Muonroi.UiEngine.Catalog.Tests;

public sealed class UiEngineCatalogControllerTests
{
    [Fact]
    public async Task GetSnapshots_ShouldResolveTenantFromExecutionContext()
    {
        Mock<ICatalogSnapshotStore> snapshotStore = new();
        snapshotStore
            .Setup(x => x.ListSnapshotsAsync("tenant-catalog", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new CatalogSnapshotSummary
                {
                    SnapshotId = Guid.NewGuid(),
                    TenantId = "tenant-catalog",
                    CapturedAt = DateTime.UtcNow,
                    ApiCount = 2,
                    RuleCount = 3,
                    BindingCount = 1
                }
            ]);

        UiEngineCatalogController controller = CreateController(
            snapshotStore: snapshotStore.Object,
            executionContextAccessor: new TestExecutionContextAccessor("tenant-catalog"));

        IReadOnlyList<CatalogSnapshotSummary> result = await controller.GetSnapshots(limit: 5, CancellationToken.None);

        CatalogSnapshotSummary summary = Assert.Single(result);
        Assert.Equal("tenant-catalog", summary.TenantId);
        snapshotStore.Verify(x => x.ListSnapshotsAsync("tenant-catalog", 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLatestSnapshot_ShouldReturnNotFound_WhenNoSnapshotExists()
    {
        Mock<ICatalogSnapshotStore> snapshotStore = new();
        snapshotStore
            .Setup(x => x.GetLatestSnapshotAsync("_global", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MUiEngineCatalogSnapshot?)null);

        UiEngineCatalogController controller = CreateController(snapshotStore: snapshotStore.Object);

        ActionResult<MUiEngineCatalogSnapshot> result = await controller.GetLatestSnapshot(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CaptureSnapshot_ShouldPersistSnapshot_AndRefreshGraphCache()
    {
        MUiEngineCatalogGraph graph = new()
        {
            Nodes =
            [
                new MUiEngineCatalogGraphNode { Id = "endpoint:GET:/catalog", Type = "endpoint", Label = "GET /catalog" },
                new MUiEngineCatalogGraphNode { Id = "rule:catalog-rule", Type = "rule", Label = "catalog-rule" }
            ],
            Edges =
            [
                new MUiEngineCatalogGraphEdge { From = "endpoint:GET:/catalog", To = "rule:catalog-rule", EdgeType = "triggers" }
            ]
        };

        MUiEngineCatalogSnapshot snapshot = new()
        {
            SnapshotId = Guid.NewGuid(),
            TenantId = "tenant-catalog",
            CapturedAt = DateTime.UtcNow,
            Graph = graph,
            ApiCount = 1,
            RuleCount = 1,
            BindingCount = 1
        };

        Mock<ICatalogScanService> scanService = new();
        scanService.Setup(x => x.BuildGraphAsync(It.IsAny<CancellationToken>())).ReturnsAsync(graph);

        Mock<ICatalogSnapshotStore> snapshotStore = new();
        snapshotStore
            .Setup(x => x.SaveSnapshotAsync("tenant-catalog", graph, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        MemoryCache cache = new(new MemoryCacheOptions());
        UiEngineCatalogController controller = CreateController(
            scanService: scanService.Object,
            snapshotStore: snapshotStore.Object,
            executionContextAccessor: new TestExecutionContextAccessor("tenant-catalog"),
            cache: cache);

        ActionResult<MUiEngineCatalogSnapshot> result = await controller.CaptureSnapshot(CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        MUiEngineCatalogSnapshot payload = Assert.IsType<MUiEngineCatalogSnapshot>(ok.Value);
        Assert.Equal(snapshot.SnapshotId, payload.SnapshotId);
        Assert.True(cache.TryGetValue("ui-engine:catalog:tenant-catalog:graph", out MUiEngineCatalogGraph? cachedGraph));
        Assert.Same(graph, cachedGraph);
    }

    private static UiEngineCatalogController CreateController(
        ICatalogScanService? scanService = null,
        ICatalogSnapshotStore? snapshotStore = null,
        ISystemExecutionContextAccessor? executionContextAccessor = null,
        IMemoryCache? cache = null)
    {
        return new UiEngineCatalogController(
            scanService ?? Mock.Of<ICatalogScanService>(),
            snapshotStore ?? Mock.Of<ICatalogSnapshotStore>(),
            executionContextAccessor ?? new TestExecutionContextAccessor(null),
            cache ?? new MemoryCache(new MemoryCacheOptions()));
    }

    private sealed class TestExecutionContextAccessor(string? tenantId) : ISystemExecutionContextAccessor
    {
        private readonly ISystemExecutionContext _context = new SystemExecutionContext(
            tenantId,
            userId: null,
            username: null,
            correlationId: Guid.NewGuid().ToString("N"),
            accessToken: null,
            apiKey: null,
            isAuthenticated: false,
            permissions: [],
            sourceType: "test");

        public ISystemExecutionContext Get() => _context;
        public void Set(ISystemExecutionContext context) => throw new NotSupportedException();
        public void Clear()
        {
        }
    }
}
