using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.Rules.Tests;

[Collection("NonParallel")]
public class FileRuleSetStoreSecurityTests
{
    [Fact]
    public async Task SaveAsync_InvalidWorkflowName_ShouldThrow()
    {
        TenantContext.CurrentTenantId = "tenant1";
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        FileRuleSetStore store = new(root);

        await Assert.ThrowsAsync<MInternalException>(() =>
            store.SaveAsync("../escape", """[{ "WorkflowName":"escape", "Rules":[] }]"""));

        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public async Task SaveAsync_RuleSetExceedsLimit_ShouldThrow()
    {
        TenantContext.CurrentTenantId = "tenant1";
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        RuleStoreConfigs configs = new()
        {
            MaxRuleSetSizeBytes = 64
        };
        FileRuleSetStore store = new(root, null, configs);
        string payload = new('x', 512);

        await Assert.ThrowsAsync<MInternalException>(() => store.SaveAsync("wf", payload));

        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public async Task GetVersionsAsync_ShouldIgnoreMalformedVersionFiles()
    {
        TenantContext.CurrentTenantId = "tenant1";
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        FileRuleSetStore store = new(root);

        await store.SaveAsync("wf", """[{ "WorkflowName":"wf", "Rules":[] }]""");
        string workflowDir = Path.Combine(root, "tenant1", "wf");
        Directory.CreateDirectory(workflowDir);
        await File.WriteAllTextAsync(Path.Combine(workflowDir, "vbad.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(workflowDir, "v-1.json"), "{}");

        int[] versions = await store.GetVersionsAsync("wf");

        Assert.Single(versions);
        Assert.Equal(1, versions[0]);

        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public async Task GetAsync_RequireSignatureWithoutSigner_ShouldThrow()
    {
        TenantContext.CurrentTenantId = "tenant1";
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        RuleStoreConfigs configs = new()
        {
            RequireSignature = true
        };

        await Assert.ThrowsAsync<MInternalException>(async () =>
        {
            FileRuleSetStore store = new(root, null, configs);
            await store.SaveAsync("wf", """[{ "WorkflowName":"wf", "Rules":[] }]""");
        });

        TenantContext.CurrentTenantId = null;
    }
}
