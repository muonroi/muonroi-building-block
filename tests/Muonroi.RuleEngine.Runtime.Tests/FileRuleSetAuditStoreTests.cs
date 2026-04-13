using FluentAssertions;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class FileRuleSetAuditStoreTests
{
    [Fact]
    public async Task QueryAsync_ShouldFilterByWorkflow_AndTenant()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        SystemExecutionContextAccessor accessor = new();
        FileRuleSetAuditStore store = new(root, new MJsonSerializeService(), accessor);

        SetTenant(accessor, "tenant-a");
        await store.AppendAsync(new RuleSetAuditEntry
        {
            WorkflowName = "wf-a",
            Action = "Save",
            Version = 1,
            TenantId = "tenant-a"
        });
        await store.AppendAsync(new RuleSetAuditEntry
        {
            WorkflowName = "wf-b",
            Action = "Activate",
            Version = 2,
            TenantId = "tenant-a"
        });

        SetTenant(accessor, "tenant-b");
        await store.AppendAsync(new RuleSetAuditEntry
        {
            WorkflowName = "wf-a",
            Action = "Save",
            Version = 1,
            TenantId = "tenant-b"
        });

        SetTenant(accessor, "tenant-a");
        RuleSetAuditPage tenantA = await store.QueryAsync();
        RuleSetAuditPage wfA = await store.QueryAsync("wf-a");

        tenantA.TotalCount.Should().Be(2);
        wfA.TotalCount.Should().Be(1);
        wfA.Items.Single().WorkflowName.Should().Be("wf-a");
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
