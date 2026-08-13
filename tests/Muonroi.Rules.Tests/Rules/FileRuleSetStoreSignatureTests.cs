using System.Security.Cryptography;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Rules.Tests.Rules;

public class FileRuleSetStoreSignatureTests : IDisposable
{
    private readonly string _rootPath;
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    public FileRuleSetStoreSignatureTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"rules-sig-test-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, true);
    }

    [Fact]
    public async Task GetAsync_WithSignature_ValidSignature_ShouldReturnContent()
    {
        var signer = new HmacSha256RuleSetSigner(_key);
        var configs = new RuleStoreConfigs { RequireSignature = true };
        var store = new FileRuleSetStore(_rootPath, signer, configs);

        await store.SaveAsync("signed-wf", "{\"data\":true}");
        string? result = await store.GetAsync("signed-wf");

        result.Should().Be("{\"data\":true}");
    }

    [Fact]
    public async Task GetAsync_WithSignature_TamperedContent_ShouldThrow()
    {
        var signer = new HmacSha256RuleSetSigner(_key);
        var configs = new RuleStoreConfigs { RequireSignature = true };
        var store = new FileRuleSetStore(_rootPath, signer, configs);

        await store.SaveAsync("tampered-wf", "{\"data\":true}");

        // Tamper with the file directly
        string tenantDir = Path.Combine(_rootPath, "default", "tampered-wf");
        string jsonPath = Path.Combine(tenantDir, "v1.json");
        await File.WriteAllTextAsync(jsonPath, "{\"data\":false}");

        Func<Task> act = () => store.GetAsync("tampered-wf");

        await act.Should().ThrowAsync<MInternalException>()
            .WithMessage("*signature validation failed*");
    }

    [Fact]
    public async Task GetAsync_WithSignature_MissingSignatureFile_ShouldThrow()
    {
        var signer = new HmacSha256RuleSetSigner(_key);
        var configs = new RuleStoreConfigs { RequireSignature = true };
        var store = new FileRuleSetStore(_rootPath, signer, configs);

        await store.SaveAsync("missingsig-wf", "{\"data\":true}");

        // Delete the signature file
        string tenantDir = Path.Combine(_rootPath, "default", "missingsig-wf");
        string sigPath = Path.Combine(tenantDir, "v1.sig");
        File.Delete(sigPath);

        Func<Task> act = () => store.GetAsync("missingsig-wf");

        await act.Should().ThrowAsync<MInternalException>()
            .WithMessage("*Signature file missing*");
    }

    [Fact]
    public async Task GetAsync_NonExistentVersion_ShouldReturnNull()
    {
        var store = new FileRuleSetStore(_rootPath);
        await store.SaveAsync("test-wf", "{\"v\":1}");

        string? result = await store.GetAsync("test-wf", 99);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_OversizeFile_ShouldThrow()
    {
        var configs = new RuleStoreConfigs { MaxRuleSetSizeBytes = 10 };
        var store = new FileRuleSetStore(_rootPath, configs: configs);

        // Manually create a file that exceeds the max size
        string tenantDir = Path.Combine(_rootPath, "default", "big-wf");
        Directory.CreateDirectory(tenantDir);
        await File.WriteAllTextAsync(Path.Combine(tenantDir, "v1.json"), new string('x', 100));
        await File.WriteAllTextAsync(Path.Combine(tenantDir, "active.txt"), "1");

        Func<Task> act = () => store.GetAsync("big-wf");

        await act.Should().ThrowAsync<MInternalException>()
            .WithMessage("*exceeds*");
    }

    [Fact]
    public async Task SaveAsync_MultipleSaves_ShouldIncrementVersions()
    {
        var store = new FileRuleSetStore(_rootPath);

        await store.SaveAsync("multi-wf", "{\"v\":1}");
        await store.SaveAsync("multi-wf", "{\"v\":2}");
        await store.SaveAsync("multi-wf", "{\"v\":3}");

        int[] versions = await store.GetVersionsAsync("multi-wf");
        versions.Should().BeEquivalentTo([1, 2, 3]);

        // Active should be latest
        string? active = await store.GetAsync("multi-wf");
        active.Should().Be("{\"v\":3}");
    }

    [Fact]
    public async Task SetActiveVersionAsync_ShouldPersist()
    {
        var store = new FileRuleSetStore(_rootPath);

        await store.SaveAsync("wf", "{\"v\":1}");
        await store.SaveAsync("wf", "{\"v\":2}");
        await store.SetActiveVersionAsync("wf", 1);

        // Re-create store to verify persistence
        var store2 = new FileRuleSetStore(_rootPath);
        string? result = await store2.GetAsync("wf");
        result.Should().Be("{\"v\":1}");
    }
}
