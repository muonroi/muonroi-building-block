using FluentAssertions;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class FileRuleSetStoreTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileRuleSetStore _sut;

    public FileRuleSetStoreTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"rulestore-{Guid.NewGuid():N}");
        _sut = new FileRuleSetStore(_rootPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, true);
    }

    [Fact]
    public async Task SaveAsync_CreatesVersionFile()
    {
        await _sut.SaveAsync("wf1", """{"rules":[{"RuleName":"r1"}]}""");

        int[] versions = await _sut.GetVersionsAsync("wf1");
        versions.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_MultipleVersions_IncrementsVersion()
    {
        await _sut.SaveAsync("wf1", """{"rules":[{"RuleName":"r1"}]}""");
        await _sut.SaveAsync("wf1", """{"rules":[{"RuleName":"r2"}]}""");

        int[] versions = await _sut.GetVersionsAsync("wf1");
        versions.Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task GetAsync_ActiveVersion_ReturnsLatest()
    {
        await _sut.SaveAsync("wf1", """{"payload":"v1"}""");
        await _sut.SaveAsync("wf1", """{"payload":"v2"}""");

        string? result = await _sut.GetAsync("wf1");
        result.Should().Contain("v2");
    }

    [Fact]
    public async Task GetAsync_SpecificVersion_ReturnsCorrectVersion()
    {
        await _sut.SaveAsync("wf1", """{"payload":"v1"}""");
        await _sut.SaveAsync("wf1", """{"payload":"v2"}""");

        string? result = await _sut.GetAsync("wf1", 1);
        result.Should().Contain("v1");
    }

    [Fact]
    public async Task GetAsync_NonExistentWorkflow_ReturnsNull()
    {
        string? result = await _sut.GetAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetActiveVersionAsync_ChangesActiveVersion()
    {
        await _sut.SaveAsync("wf1", """{"payload":"v1"}""");
        await _sut.SaveAsync("wf1", """{"payload":"v2"}""");

        await _sut.SetActiveVersionAsync("wf1", 1);
        string? result = await _sut.GetAsync("wf1");

        result.Should().Contain("v1");
    }

    [Fact]
    public async Task SetActiveVersionAsync_VersionZero_Throws()
    {
        Func<Task> act = () => _sut.SetActiveVersionAsync("wf1", 0);

        await act.Should().ThrowAsync<MArgumentException>();
    }

    [Fact]
    public async Task SetActiveVersionAsync_NonExistentVersion_Throws()
    {
        await _sut.SaveAsync("wf1", """{"payload":"v1"}""");

        Func<Task> act = () => _sut.SetActiveVersionAsync("wf1", 99);

        await act.Should().ThrowAsync<MNotFoundException>();
    }

    [Fact]
    public async Task GetVersionsAsync_NonExistentWorkflow_ReturnsEmpty()
    {
        int[] versions = await _sut.GetVersionsAsync("nonexistent");

        versions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveVersionAsync_NoActive_ReturnsLatest()
    {
        await _sut.SaveAsync("wf1", """{"payload":"v1"}""");

        int? active = await _sut.GetActiveVersionAsync("wf1");

        active.Should().Be(1);
    }

    [Fact]
    public async Task GetActiveVersionAsync_NonExistentWorkflow_ReturnsNull()
    {
        int? active = await _sut.GetActiveVersionAsync("nonexistent");

        active.Should().BeNull();
    }

    [Fact]
    public async Task GetWorkflowsAsync_ReturnsWorkflowNames()
    {
        await _sut.SaveAsync("wf-alpha", """{"payload":"a"}""");
        await _sut.SaveAsync("wf-beta", """{"payload":"b"}""");

        IReadOnlyList<string> workflows = await _sut.GetWorkflowsAsync();

        workflows.Should().Contain("wf-alpha");
        workflows.Should().Contain("wf-beta");
    }

    [Fact]
    public async Task GetWorkflowsAsync_NoWorkflows_ReturnsEmpty()
    {
        IReadOnlyList<string> workflows = await _sut.GetWorkflowsAsync();

        workflows.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_EmptyRootPath_Throws()
    {
        Action act = () => new FileRuleSetStore("");

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Constructor_ZeroMaxSize_Throws()
    {
        RuleStoreConfigs configs = new() { MaxRuleSetSizeBytes = 0 };

        Action act = () => new FileRuleSetStore(_rootPath, configs: Options.Create(configs));

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Constructor_RequireSignatureNoSigner_Throws()
    {
        RuleStoreConfigs configs = new() { RequireSignature = true };

        Action act = () => new FileRuleSetStore(_rootPath, configs: Options.Create(configs));

        act.Should().Throw<MConfigurationException>();
    }

    [Fact]
    public async Task SaveAsync_WithSigner_CreatesSignatureFile()
    {
        byte[] key = System.Text.Encoding.UTF8.GetBytes("test-key-12345678");
        HmacSha256RuleSetSigner signer = new(key);
        FileRuleSetStore store = new(_rootPath, signer: signer);

        await store.SaveAsync("wf1", """{"payload":"v1"}""");

        string? content = await store.GetAsync("wf1");
        content.Should().Contain("v1");
    }

    [Fact]
    public async Task SaveAsync_ExceedsMaxSize_Throws()
    {
        RuleStoreConfigs configs = new() { MaxRuleSetSizeBytes = 10 };
        FileRuleSetStore store = new(_rootPath, configs: Options.Create(configs));
        string largeJson = new('x', 100);

        Func<Task> act = () => store.SaveAsync("wf1", largeJson);

        await act.Should().ThrowAsync<MConfigurationException>();
    }

    [Fact]
    public async Task SaveAsync_NullJson_Throws()
    {
        Func<Task> act = () => _sut.SaveAsync("wf1", null!);

        await act.Should().ThrowAsync<MArgumentException>();
    }

    [Fact]
    public async Task SaveAsync_InvalidPathCharacters_Throws()
    {
        Func<Task> act = () => _sut.SaveAsync("../../../etc", """{"p":"v"}""");

        await act.Should().ThrowAsync<MConfigurationException>();
    }
}
