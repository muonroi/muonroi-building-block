using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Enterprise;
using Muonroi.Pdf.Enterprise.Extensions;
using Muonroi.Pdf.Enterprise.Metering;
using Muonroi.Quota.Abstractions;
using Muonroi.Tenancy.Core;

namespace Muonroi.Pdf.Governance.Tests;

/// <summary>
/// D-02 coverage: verifies EnterprisePdfServiceWrapper meters page count per render,
/// never blocks on metering failure, skips when no tenant, and is bound as the active IMPdfService.
/// Also verifies QuotaType.PdfRendersPerDay via InMemoryTenantQuotaTracker.GetLimit (Task 1 behaviors).
/// </summary>
public sealed class EnterprisePdfServiceWrapperTests
{
    // ─── hand-written fakes ───────────────────────────────────────────────────

    private sealed class FakeInnerPdfService : IMPdfService
    {
        public int PageCount { get; set; } = 4;

        public Task<PdfRenderResult> RenderAsync(
            string html,
            Stream destination,
            PdfRenderOptions options,
            CancellationToken cancellationToken = default)
        {
            PdfRenderResult result = new(PageCount, 1024L, TimeSpan.FromMilliseconds(10), "hash", "policy-id", []);
            return Task.FromResult(result);
        }

        public Task<PdfRenderResult> RenderMultiPageAsync(
            IReadOnlyList<string> htmlPages,
            Stream destination,
            PdfRenderOptions options,
            CancellationToken cancellationToken = default)
        {
            PdfRenderResult result = new(PageCount, 2048L, TimeSpan.FromMilliseconds(20), "hash", "policy-id", []);
            return Task.FromResult(result);
        }

        public Task<(byte[] Bytes, PdfRenderResult Metadata)> RenderToBytesAsync(
            string html,
            PdfRenderOptions options,
            CancellationToken cancellationToken = default)
        {
            PdfRenderResult result = new(PageCount, 512L, TimeSpan.FromMilliseconds(5), "hash", "policy-id", []);
            return Task.FromResult((new byte[] { 0x25, 0x50, 0x44, 0x46 }, result));
        }
    }

    private sealed class RecordingQuotaTracker : ITenantQuotaTracker
    {
        public List<(string TenantId, QuotaType Type, int Amount)> Calls { get; } = new();

        public Task IncrementUsageAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default)
        {
            Calls.Add((tenantId, type, amount));
            return Task.CompletedTask;
        }

        public Task<bool> CheckQuotaAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default)
            => Task.FromResult(new QuotaUsage());

        public Task ResetDailyQuotasAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class ThrowingQuotaTracker : ITenantQuotaTracker
    {
        public Task IncrementUsageAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default)
            => throw new InvalidOperationException("Simulated metering failure");

        public Task<bool> CheckQuotaAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default)
            => Task.FromResult(new QuotaUsage());

        public Task ResetDailyQuotasAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeExecutionContextAccessor : ISystemExecutionContextAccessor
    {
        private readonly string? _tenantId;

        public FakeExecutionContextAccessor(string? tenantId) => _tenantId = tenantId;

        public ISystemExecutionContext Get() => new SystemExecutionContext(
            _tenantId, null, null, "corr-123", null, null, false, [], "test");

        public void Set(ISystemExecutionContext context) { }
        public void Clear() { }
    }

    private sealed class FakeQuotaStore : ITenantQuotaStore
    {
        private readonly Dictionary<string, QuotaUsage> _usage = new();

        public Task<TenantQuota?> GetQuotaAsync(string tenantId, CancellationToken ct = default)
            => Task.FromResult<TenantQuota?>(new TenantQuota { TenantId = tenantId });

        public Task<QuotaUsage> GetUsageAsync(string tenantId, CancellationToken ct = default)
        {
            if (!_usage.TryGetValue(tenantId, out QuotaUsage? usage))
            {
                usage = new QuotaUsage();
                _usage[tenantId] = usage;
            }

            return Task.FromResult(usage);
        }

        public Task SaveQuotaAsync(string tenantId, TenantQuota quota, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RecordUsageAsync(string tenantId, QuotaType type, int amount, CancellationToken ct = default)
        {
            if (!_usage.TryGetValue(tenantId, out QuotaUsage? usage))
            {
                usage = new QuotaUsage();
                _usage[tenantId] = usage;
            }

            usage.CurrentUsage.TryGetValue(type, out int current);
            usage.CurrentUsage[type] = current + amount;
            return Task.CompletedTask;
        }

        public Task ResetDailyCountersAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    // ─── Task 1, Test 1: GetLimit for PdfRendersPerDay returns int.MaxValue (unlimited) ──

    [Fact]
    public async Task GetLimit_PdfRendersPerDay_DefaultQuota_ReturnsIntMaxValue()
    {
        // Arrange: default TenantQuota — MaxPdfRendersPerDay defaults to int.MaxValue
        FakeQuotaStore store = new();
        InMemoryTenantQuotaTracker tracker = new(store);
        string tenantId = "test-tenant";

        // Act: CheckQuotaAsync uses GetLimit internally; unlimited returns true regardless of amount
        bool allowed = await tracker.CheckQuotaAsync(tenantId, QuotaType.PdfRendersPerDay, int.MaxValue - 1);

        // Assert: unlimited — GetLimit returns int.MaxValue which short-circuits to true
        Assert.True(allowed, "Default TenantQuota.MaxPdfRendersPerDay should be int.MaxValue (unlimited)");
    }

    // ─── Task 1, Test 2: IncrementUsageAsync for PdfRendersPerDay records usage without throw ──

    [Fact]
    public async Task IncrementUsageAsync_PdfRendersPerDay_RecordsUsageWithoutThrow()
    {
        // Arrange
        FakeQuotaStore store = new();
        InMemoryTenantQuotaTracker tracker = new(store);
        string tenantId = "tenant-quota-test";

        // Act
        await tracker.IncrementUsageAsync(tenantId, QuotaType.PdfRendersPerDay, 3);

        // Assert: usage is recorded as 3
        QuotaUsage usage = await store.GetUsageAsync(tenantId);
        Assert.True(usage.CurrentUsage.TryGetValue(QuotaType.PdfRendersPerDay, out int recorded));
        Assert.Equal(3, recorded);
    }

    // ─── Task 2, Test 1 (D-02 record): wrapper meters page count with correct tenant + type + amount ──

    [Fact]
    public async Task RenderAsync_WithTenantContext_RecordsMeteringWithCorrectArgs()
    {
        // Arrange
        FakeInnerPdfService inner = new() { PageCount = 4 };
        RecordingQuotaTracker tracker = new();
        FakeExecutionContextAccessor accessor = new("t1");
        EnterprisePdfServiceWrapper wrapper = new(inner, tracker, accessor);

        using MemoryStream dest = new();
        PdfRenderOptions options = new();

        // Act
        PdfRenderResult result = await wrapper.RenderAsync("<html/>", dest, options);

        // Assert
        Assert.Single(tracker.Calls);
        (string tenantId, QuotaType type, int amount) = tracker.Calls[0];
        Assert.Equal("t1", tenantId);
        Assert.Equal(QuotaType.PdfRendersPerDay, type);
        Assert.Equal(4, amount);
        Assert.Equal(4, result.PageCount); // inner result returned verbatim
    }

    // ─── Task 2, Test 2 (non-blocking failure): throwing tracker does NOT propagate ──

    [Fact]
    public async Task RenderAsync_TrackerThrows_DoesNotPropagateException()
    {
        // Arrange
        FakeInnerPdfService inner = new() { PageCount = 2 };
        ThrowingQuotaTracker tracker = new();
        FakeExecutionContextAccessor accessor = new("tenant-x");
        EnterprisePdfServiceWrapper wrapper = new(inner, tracker, accessor);

        using MemoryStream dest = new();
        PdfRenderOptions options = new();

        // Act: must not throw even though tracker throws
        PdfRenderResult result = await wrapper.RenderAsync("<html/>", dest, options);

        // Assert: inner result is still returned
        Assert.Equal(2, result.PageCount);
    }

    // ─── Task 2, Test 3 (no tenant): no tenant = tracker NOT called ──

    [Fact]
    public async Task RenderAsync_NoTenant_TrackerNotCalled()
    {
        // Arrange: accessor returns null TenantId; TenantContext.CurrentTenantId also null
        FakeInnerPdfService inner = new() { PageCount = 1 };
        RecordingQuotaTracker tracker = new();
        FakeExecutionContextAccessor accessor = new(null);

        // Ensure AsyncLocal is clear for this test
        TenantContext.CurrentTenantId = null;

        EnterprisePdfServiceWrapper wrapper = new(inner, tracker, accessor);

        using MemoryStream dest = new();
        PdfRenderOptions options = new();

        // Act
        PdfRenderResult result = await wrapper.RenderAsync("<html/>", dest, options);

        // Assert: tracker receives no calls when tenant is null/empty
        Assert.Empty(tracker.Calls);
        Assert.Equal(1, result.PageCount);
    }

    // ─── Task 2, Test 4 (SC3 active wiring): AddPdfEnterprise resolves EnterprisePdfServiceWrapper ──

    [Fact]
    public void AddPdfEnterprise_ResolvesEnterprisePdfServiceWrapperAsActiveIMPdfService()
    {
        // Arrange: register inner IMPdfService stub + stub ITenantQuotaTracker, then AddPdfEnterprise
        ServiceCollection services = new();
        services.AddSingleton<IMPdfService, FakeInnerPdfService>();
        services.AddSingleton<ITenantQuotaTracker, RecordingQuotaTracker>();

        // LicenseFeatureGate requires ILicenseGuard — not needed for this test; use a stub
        // AddPdfEnterprise also registers LicenseFeatureGate via TryAddSingleton.
        // To avoid ILicenseGuard resolution failure, skip gate test — only test IMPdfService binding.
        services.AddPdfEnterprise();

        // Act: build the provider and resolve the active IMPdfService
        using ServiceProvider sp = services.BuildServiceProvider();
        IMPdfService resolved = sp.GetRequiredService<IMPdfService>();

        // Assert: the active binding is the wrapper (SC3 metering is on the render path)
        Assert.IsType<EnterprisePdfServiceWrapper>(resolved);
    }
}
