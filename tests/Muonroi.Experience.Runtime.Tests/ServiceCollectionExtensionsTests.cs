using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Experience.Abstractions;
using Muonroi.Experience.Runtime;
using Muonroi.Experience.Runtime.File;
using Muonroi.Experience.Runtime.Qdrant;
using Xunit;

namespace Muonroi.Experience.Runtime.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    // -------------------------------------------------------------------------
    // Test 1: File store type resolves FileExperienceStore
    // -------------------------------------------------------------------------

    [Fact]
    public void AddExperienceStore_FileType_ResolvesFileExperienceStore()
    {
        ServiceCollection services = new();
        services.AddExperienceStore(opts =>
        {
            opts.StoreType = ExperienceStoreType.File;
            opts.FileDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        IExperienceStore store = provider.GetRequiredService<IExperienceStore>();

        store.Should().BeOfType<FileExperienceStore>();
    }

    // -------------------------------------------------------------------------
    // Test 2: Qdrant store type resolves QdrantExperienceStore
    // -------------------------------------------------------------------------

    [Fact]
    public void AddExperienceStore_QdrantType_ResolvesQdrantExperienceStore()
    {
        ServiceCollection services = new();
        services.AddExperienceStore(opts =>
        {
            opts.StoreType = ExperienceStoreType.Qdrant;
            opts.VectorSize = 4;
            opts.QdrantUrl = "http://localhost:6334";
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        IExperienceStore store = provider.GetRequiredService<IExperienceStore>();

        store.Should().BeOfType<QdrantExperienceStore>();
    }

    // -------------------------------------------------------------------------
    // Test 3: Default options use File store
    // -------------------------------------------------------------------------

    [Fact]
    public void AddExperienceStore_DefaultOptions_UsesFileStore()
    {
        ServiceCollection services = new();
        services.AddExperienceStore();

        using ServiceProvider provider = services.BuildServiceProvider();
        IExperienceStore store = provider.GetRequiredService<IExperienceStore>();

        store.Should().BeOfType<FileExperienceStore>();
    }

    // -------------------------------------------------------------------------
    // Test 4: Orchestrator is registered
    // -------------------------------------------------------------------------

    [Fact]
    public void AddExperienceStore_RegistersOrchestrator()
    {
        ServiceCollection services = new();
        services.AddExperienceStore(opts =>
        {
            opts.StoreType = ExperienceStoreType.File;
            opts.FileDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        ExperienceStoreOrchestrator orchestrator = provider.GetRequiredService<ExperienceStoreOrchestrator>();

        orchestrator.Should().NotBeNull();
    }
}
