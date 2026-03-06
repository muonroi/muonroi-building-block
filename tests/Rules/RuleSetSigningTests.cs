namespace Muonroi.Rules.Tests;

[Collection("NonParallel")]
public class RuleSetSigningTests
{
    private sealed class PassthroughSigner : IRuleSetSigner
    {
        public string Sign(string content)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        }

        public bool Verify(string content, string signature)
        {
            return Sign(content) == signature;
        }
    }

    [Fact]
    public async Task GetAsync_Verifies_Signature()
    {
        TenantContext.CurrentTenantId = string.Empty;
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        FileRuleSetStore store = new(dir, new PassthroughSigner());
        const string json = "[{\"WorkflowName\":\"wf\",\"Rules\":[]}]";

        await store.SaveAsync("wf", json);

        string? loaded = await store.GetAsync("wf");
        Assert.Equal(json, loaded);

        string file = Path.Combine(dir, "default", "wf", "v1.json");
        await File.WriteAllTextAsync(file, json.Replace("wf", "tamper"));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetAsync("wf"));

        TenantContext.CurrentTenantId = null;
    }
}
