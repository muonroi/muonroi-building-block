using FluentAssertions;
using Muonroi.Core.Abstractions.Context;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class FileRuleSetStoreMetadataTests
{
    [Fact]
    public async Task GetActiveVersionAsync_ShouldReturnCurrentActive()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        SystemExecutionContextAccessor accessor = new();
        FileRuleSetStore store = new(root, executionContextAccessor: accessor);

        SetTenant(accessor, "tenant-a");
        await store.SaveAsync("workflow-a", "{\"workflowName\":\"workflow-a\",\"rules\":[\"A\"]}");
        await store.SaveAsync("workflow-a", "{\"workflowName\":\"workflow-a\",\"rules\":[\"A\",\"B\"]}");
        await store.SetActiveVersionAsync("workflow-a", 1);

        int? activeVersion = await store.GetActiveVersionAsync("workflow-a");
        int[] versions = await store.GetVersionsAsync("workflow-a");

        activeVersion.Should().Be(1);
        versions.Should().Equal([1, 2]);
        accessor.Clear();
    }

    [Fact]
    public async Task GetWorkflowsAsync_ShouldReturnCurrentTenantOnly()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        SystemExecutionContextAccessor accessor = new();
        FileRuleSetStore store = new(root, executionContextAccessor: accessor);

        SetTenant(accessor, "tenant-a");
        await store.SaveAsync("workflow-a", "{\"workflowName\":\"workflow-a\",\"rules\":[\"A\"]}");
        await store.SaveAsync("workflow-b", "{\"workflowName\":\"workflow-b\",\"rules\":[\"B\"]}");

        SetTenant(accessor, "tenant-b");
        await store.SaveAsync("workflow-c", "{\"workflowName\":\"workflow-c\",\"rules\":[\"C\"]}");

        SetTenant(accessor, "tenant-a");
        IReadOnlyList<string> workflowsA = await store.GetWorkflowsAsync();

        SetTenant(accessor, "tenant-b");
        IReadOnlyList<string> workflowsB = await store.GetWorkflowsAsync();

        workflowsA.Should().BeEquivalentTo(["workflow-a", "workflow-b"]);
        workflowsB.Should().BeEquivalentTo(["workflow-c"]);
        accessor.Clear();
    }

    private static void SetTenant(ISystemExecutionContextAccessor accessor, string tenantId)
    {
        accessor.Set(new SystemExecutionContext(
            tenantId,
            userId: null,
            username: null,
            correlationId: Guid.NewGuid().ToString("N"),
            accessToken: null,
            apiKey: null,
            isAuthenticated: false,
            permissions: [],
            sourceType: "tests"));
    }
}
