using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.License;
using Muonroi.Governance.Enterprise.ServerValidation;

namespace Muonroi.Governance.Enterprise.Tests.ServerValidation;

public sealed class FileFailedChainSubmissionStoreTests : IDisposable
{
    private readonly string _root;
    private readonly FileFailedChainSubmissionStore _store;

    public FileFailedChainSubmissionStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Muonroi.FailedChain-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        LicenseConfigs configs = new() { ChainFilePath = Path.Combine(_root, "chain", "chain.ndjson") };
        _store = new FileFailedChainSubmissionStore(environment: null, configs, new MJsonSerializeService());
    }

    [Fact]
    public async Task Enqueue_Then_List_RoundTrips()
    {
        PendingChainSubmission pending = new()
        {
            Id = "abc123",
            AttemptCount = 1,
            FirstFailedAtUtc = DateTimeOffset.UtcNow,
            Request = new ChainSubmissionRequest
            {
                TenantId = "TenantA",
                Entries = [new FingerprintChainEntry { TenantId = "TenantA", ActionType = "api.list" }]
            }
        };

        await _store.EnqueueAsync(pending);

        IReadOnlyList<PendingChainSubmission> listed = await _store.ListPendingAsync();
        Assert.Single(listed);
        Assert.Equal("abc123", listed[0].Id);
        Assert.Equal("TenantA", listed[0].Request.TenantId);
        Assert.Single(listed[0].Request.Entries);
    }

    [Fact]
    public async Task Update_OverwritesExisting_NoDuplicate()
    {
        PendingChainSubmission pending = new() { Id = "p1", AttemptCount = 1 };
        await _store.EnqueueAsync(pending);

        pending.AttemptCount = 3;
        pending.LastError = "still failing";
        await _store.UpdateAsync(pending);

        IReadOnlyList<PendingChainSubmission> listed = await _store.ListPendingAsync();
        Assert.Single(listed);
        Assert.Equal(3, listed[0].AttemptCount);
        Assert.Equal("still failing", listed[0].LastError);
    }

    [Fact]
    public async Task Remove_DeletesEntry()
    {
        await _store.EnqueueAsync(new PendingChainSubmission { Id = "p1" });
        await _store.EnqueueAsync(new PendingChainSubmission { Id = "p2" });

        await _store.RemoveAsync("p1");

        IReadOnlyList<PendingChainSubmission> listed = await _store.ListPendingAsync();
        Assert.Single(listed);
        Assert.Equal("p2", listed[0].Id);
    }

    [Fact]
    public async Task Enqueue_WithoutId_AssignsId()
    {
        PendingChainSubmission pending = new();
        await _store.EnqueueAsync(pending);

        Assert.False(string.IsNullOrWhiteSpace(pending.Id));
        IReadOnlyList<PendingChainSubmission> listed = await _store.ListPendingAsync();
        Assert.Single(listed);
    }

    [Fact]
    public async Task List_OnEmptyStore_ReturnsEmpty()
    {
        IReadOnlyList<PendingChainSubmission> listed = await _store.ListPendingAsync();
        Assert.Empty(listed);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
