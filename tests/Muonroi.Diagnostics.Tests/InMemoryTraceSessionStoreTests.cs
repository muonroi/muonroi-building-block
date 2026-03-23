using FluentAssertions;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Diagnostics.Store;
using Xunit;

namespace Muonroi.Diagnostics.Tests;

public class InMemoryTraceSessionStoreTests
{
    private readonly InMemoryTraceSessionStore _store = new();

    private static MTraceSessionRecord CreateSession(string id, string? tenantId = "t1", DateTime? startedAt = null)
    {
        return new MTraceSessionRecord
        {
            SessionId = id,
            TenantId = tenantId,
            StartedAt = startedAt ?? DateTime.UtcNow,
            Nodes = []
        };
    }

    [Fact]
    public async Task SaveAsync_Then_GetAsync_Should_Return_Session()
    {
        MTraceSessionRecord session = CreateSession("s1");
        await _store.SaveAsync(session);

        MTraceSessionRecord? retrieved = await _store.GetAsync("s1", "t1");

        retrieved.Should().NotBeNull();
        retrieved!.SessionId.Should().Be("s1");
    }

    [Fact]
    public async Task GetAsync_NonExistent_Should_Return_Null()
    {
        MTraceSessionRecord? result = await _store.GetAsync("nonexistent", "t1");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_Should_Overwrite_Existing()
    {
        MTraceSessionRecord session1 = new()
        {
            SessionId = "s1",
            TenantId = "t1",
            StartedAt = DateTime.UtcNow,
            Nodes = [],
            UserId = "user-1"
        };
        await _store.SaveAsync(session1);

        MTraceSessionRecord session2 = new()
        {
            SessionId = "s1",
            TenantId = "t1",
            StartedAt = DateTime.UtcNow,
            Nodes = [],
            UserId = "user-2"
        };
        await _store.SaveAsync(session2);

        MTraceSessionRecord? result = await _store.GetAsync("s1", "t1");
        result!.UserId.Should().Be("user-2");
    }

    [Fact]
    public async Task GetAsync_With_Null_TenantId_Should_Use_Global_Key()
    {
        MTraceSessionRecord session = CreateSession("s1", tenantId: null);
        await _store.SaveAsync(session);

        MTraceSessionRecord? result = await _store.GetAsync("s1", null);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task QueryByTenantAsync_Should_Return_Sessions_In_Time_Range()
    {
        DateTime now = DateTime.UtcNow;
        await _store.SaveAsync(CreateSession("s1", "t1", now.AddMinutes(-5)));
        await _store.SaveAsync(CreateSession("s2", "t1", now));
        await _store.SaveAsync(CreateSession("s3", "t2", now)); // different tenant

        IReadOnlyList<MTraceSessionRecord> results = await _store.QueryByTenantAsync(
            "t1", now.AddMinutes(-10), now.AddMinutes(1));

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.TenantId == "t1");
    }

    [Fact]
    public async Task QueryByTenantAsync_Should_Respect_MaxResults()
    {
        DateTime now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            await _store.SaveAsync(CreateSession($"s{i}", "t1", now.AddMinutes(-i)));
        }

        IReadOnlyList<MTraceSessionRecord> results = await _store.QueryByTenantAsync(
            "t1", now.AddMinutes(-10), now.AddMinutes(1), maxResults: 3);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task QueryByTenantAsync_Should_Return_Ordered_By_StartedAt_Descending()
    {
        DateTime now = DateTime.UtcNow;
        await _store.SaveAsync(CreateSession("old", "t1", now.AddMinutes(-10)));
        await _store.SaveAsync(CreateSession("new", "t1", now));

        IReadOnlyList<MTraceSessionRecord> results = await _store.QueryByTenantAsync(
            "t1", now.AddMinutes(-20), now.AddMinutes(1));

        results[0].SessionId.Should().Be("new");
        results[1].SessionId.Should().Be("old");
    }

    [Fact]
    public async Task QueryByTenantAsync_Outside_Range_Should_Return_Empty()
    {
        DateTime now = DateTime.UtcNow;
        await _store.SaveAsync(CreateSession("s1", "t1", now));

        IReadOnlyList<MTraceSessionRecord> results = await _store.QueryByTenantAsync(
            "t1", now.AddHours(-2), now.AddHours(-1));

        results.Should().BeEmpty();
    }
}
