using System.Security.Cryptography;

namespace Muonroi.Rules.Tests.Rules;

public class FileRuleSetStoreTests : IDisposable
{
    private readonly string _rootPath;

    public FileRuleSetStoreTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"rules-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }

    [Fact]
    public void Constructor_EmptyRootPath_ShouldThrow()
    {
        Action act = () => new FileRuleSetStore("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullRootPath_ShouldThrow()
    {
        Action act = () => new FileRuleSetStore(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_InvalidMaxSize_ShouldThrow()
    {
        var configs = new RuleStoreConfigs { MaxRuleSetSizeBytes = 0 };
        Action act = () => new FileRuleSetStore(_rootPath, configs: configs);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_RequireSignatureWithoutSigner_ShouldThrow()
    {
        var configs = new RuleStoreConfigs { RequireSignature = true };
        Action act = () => new FileRuleSetStore(_rootPath, signer: null, configs: configs);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistAndRetrieve()
    {
        var store = new FileRuleSetStore(_rootPath);

        await store.SaveAsync("test-wf", "{\"data\":1}");
        string? result = await store.GetAsync("test-wf");

        result.Should().Be("{\"data\":1}");
    }

    [Fact]
    public async Task SaveAsync_MultipleSaves_ShouldCreateVersions()
    {
        var store = new FileRuleSetStore(_rootPath);

        await store.SaveAsync("test-wf", "{\"v\":1}");
        await store.SaveAsync("test-wf", "{\"v\":2}");

        int[] versions = await store.GetVersionsAsync("test-wf");
        versions.Should().BeEquivalentTo([1, 2]);

        // Latest should be active
        string? latest = await store.GetAsync("test-wf");
        latest.Should().Be("{\"v\":2}");
    }

    [Fact]
    public async Task GetAsync_SpecificVersion_ShouldReturnCorrectData()
    {
        var store = new FileRuleSetStore(_rootPath);

        await store.SaveAsync("test-wf", "{\"v\":1}");
        await store.SaveAsync("test-wf", "{\"v\":2}");

        string? v1 = await store.GetAsync("test-wf", 1);
        v1.Should().Be("{\"v\":1}");
    }

    [Fact]
    public async Task GetAsync_NonExistentWorkflow_ShouldReturnNull()
    {
        var store = new FileRuleSetStore(_rootPath);

        string? result = await store.GetAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetActiveVersionAsync_ShouldChangeActiveVersion()
    {
        var store = new FileRuleSetStore(_rootPath);

        await store.SaveAsync("test-wf", "{\"v\":1}");
        await store.SaveAsync("test-wf", "{\"v\":2}");
        await store.SetActiveVersionAsync("test-wf", 1);

        string? result = await store.GetAsync("test-wf");
        result.Should().Be("{\"v\":1}");
    }

    [Fact]
    public async Task SetActiveVersionAsync_InvalidVersion_ShouldThrow()
    {
        var store = new FileRuleSetStore(_rootPath);

        Func<Task> act = () => store.SetActiveVersionAsync("test-wf", 0);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SetActiveVersionAsync_NonExistentVersion_ShouldThrow()
    {
        var store = new FileRuleSetStore(_rootPath);
        await store.SaveAsync("test-wf", "{\"v\":1}");

        Func<Task> act = () => store.SetActiveVersionAsync("test-wf", 99);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task GetVersionsAsync_NonExistentWorkflow_ShouldReturnEmpty()
    {
        var store = new FileRuleSetStore(_rootPath);

        int[] versions = await store.GetVersionsAsync("nonexistent");
        versions.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WithSigner_ShouldSignAndVerify()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        var signer = new HmacSha256RuleSetSigner(key);
        var configs = new RuleStoreConfigs { RequireSignature = true };
        var store = new FileRuleSetStore(_rootPath, signer, configs);

        await store.SaveAsync("test-wf", "{\"signed\":true}");

        string? result = await store.GetAsync("test-wf");
        result.Should().Be("{\"signed\":true}");
    }

    [Fact]
    public async Task SaveAsync_ExceedsMaxSize_ShouldThrow()
    {
        var configs = new RuleStoreConfigs { MaxRuleSetSizeBytes = 10 };
        var store = new FileRuleSetStore(_rootPath, configs: configs);

        Func<Task> act = () => store.SaveAsync("test-wf", new string('x', 100));
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task SaveAsync_PathTraversal_ShouldThrow()
    {
        var store = new FileRuleSetStore(_rootPath);

        Func<Task> act = () => store.SaveAsync("../../../etc/passwd", "{}");
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task SaveAsync_InvalidWorkflowName_ShouldThrow()
    {
        var store = new FileRuleSetStore(_rootPath);

        Func<Task> act = () => store.SaveAsync("invalid name with spaces!", "{}");
        await act.Should().ThrowAsync<InvalidDataException>();
    }
}
