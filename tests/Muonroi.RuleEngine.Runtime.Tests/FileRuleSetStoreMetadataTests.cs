using FluentAssertions;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.Tenancy.Core;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class FileRuleSetStoreMetadataTests
{
    [Fact]
    public async Task GetActiveVersionAsync_ShouldReturnCurrentActive()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FileRuleSetStore store = new(root);

        TenantContext.CurrentTenantId = "tenant-a";
        await store.SaveAsync("workflow-a", "{\"workflowName\":\"workflow-a\",\"rules\":[\"A\"]}");
        await store.SaveAsync("workflow-a", "{\"workflowName\":\"workflow-a\",\"rules\":[\"A\",\"B\"]}");
        await store.SetActiveVersionAsync("workflow-a", 1);

        int? activeVersion = await store.GetActiveVersionAsync("workflow-a");
        int[] versions = await store.GetVersionsAsync("workflow-a");

        activeVersion.Should().Be(1);
        versions.Should().Equal([1, 2]);
        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public async Task GetWorkflowsAsync_ShouldReturnCurrentTenantOnly()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FileRuleSetStore store = new(root);

        TenantContext.CurrentTenantId = "tenant-a";
        await store.SaveAsync("workflow-a", "{\"workflowName\":\"workflow-a\",\"rules\":[\"A\"]}");
        await store.SaveAsync("workflow-b", "{\"workflowName\":\"workflow-b\",\"rules\":[\"B\"]}");

        TenantContext.CurrentTenantId = "tenant-b";
        await store.SaveAsync("workflow-c", "{\"workflowName\":\"workflow-c\",\"rules\":[\"C\"]}");

        TenantContext.CurrentTenantId = "tenant-a";
        IReadOnlyList<string> workflowsA = await store.GetWorkflowsAsync();

        TenantContext.CurrentTenantId = "tenant-b";
        IReadOnlyList<string> workflowsB = await store.GetWorkflowsAsync();

        workflowsA.Should().BeEquivalentTo(["workflow-a", "workflow-b"]);
        workflowsB.Should().BeEquivalentTo(["workflow-c"]);
        TenantContext.CurrentTenantId = null;
    }
}
