using FluentAssertions;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.Tenancy.Core;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class FileRuleSetAuditStoreTests
{
    [Fact]
    public async Task QueryAsync_ShouldFilterByWorkflow_AndTenant()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FileRuleSetAuditStore store = new(root, new MJsonSerializeService());

        TenantContext.CurrentTenantId = "tenant-a";
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

        TenantContext.CurrentTenantId = "tenant-b";
        await store.AppendAsync(new RuleSetAuditEntry
        {
            WorkflowName = "wf-a",
            Action = "Save",
            Version = 1,
            TenantId = "tenant-b"
        });

        TenantContext.CurrentTenantId = "tenant-a";
        RuleSetAuditPage tenantA = await store.QueryAsync();
        RuleSetAuditPage wfA = await store.QueryAsync("wf-a");

        tenantA.TotalCount.Should().Be(2);
        wfA.TotalCount.Should().Be(1);
        wfA.Items.Single().WorkflowName.Should().Be("wf-a");
        TenantContext.CurrentTenantId = null;
    }
}
