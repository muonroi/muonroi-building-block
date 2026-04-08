using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Muonroi.Experience.Abstractions;
using Muonroi.Experience.Runtime;
using Muonroi.Experience.Runtime.Brain;
using Muonroi.Experience.Runtime.Extraction;
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

    // =========================================================================
    // AddExperienceBrain tests
    // =========================================================================

    private static ServiceCollection BuildServicesWithBrain(Action<ExperienceBrainOptions>? configureBrain = null)
    {
        ServiceCollection services = new();
        services.AddExperienceStore(opts =>
        {
            opts.StoreType = ExperienceStoreType.File;
            opts.FileDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        });
        services.AddExperienceBrain(configureBrain);
        return services;
    }

    // -------------------------------------------------------------------------
    // Test 5: AddExperienceBrain resolves IExperienceBrain as CompositeExperienceBrain
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Extraction")]
    public void AddExperienceBrain_RegistersCompositeAsIExperienceBrain()
    {
        ServiceCollection services = BuildServicesWithBrain();

        using ServiceProvider provider = services.BuildServiceProvider();
        IExperienceBrain brain = provider.GetRequiredService<IExperienceBrain>();

        brain.Should().BeOfType<CompositeExperienceBrain>();
    }

    // -------------------------------------------------------------------------
    // Test 6: AddExperienceBrain resolves MistakeDetector
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Extraction")]
    public void AddExperienceBrain_RegistersMistakeDetector()
    {
        ServiceCollection services = BuildServicesWithBrain();

        using ServiceProvider provider = services.BuildServiceProvider();
        MistakeDetector detector = provider.GetRequiredService<MistakeDetector>();

        detector.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // Test 7: AddExperienceBrain resolves IExperienceExtractor as ExperienceExtractionPipeline
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Extraction")]
    public void AddExperienceBrain_RegistersExtractionPipelineAsIExperienceExtractor()
    {
        ServiceCollection services = BuildServicesWithBrain();

        using ServiceProvider provider = services.BuildServiceProvider();
        IExperienceExtractor extractor = provider.GetRequiredService<IExperienceExtractor>();

        extractor.Should().BeOfType<ExperienceExtractionPipeline>();
    }

    // -------------------------------------------------------------------------
    // Test 8: AddExperienceBrain applies configure delegate to ExperienceBrainOptions
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Extraction")]
    public void AddExperienceBrain_ConfiguresOptions()
    {
        const string expectedApiKey = "test-api-key-12345";
        ServiceCollection services = BuildServicesWithBrain(opts => opts.ClaudeApiKey = expectedApiKey);

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptions<ExperienceBrainOptions> options = provider.GetRequiredService<IOptions<ExperienceBrainOptions>>();

        options.Value.ClaudeApiKey.Should().Be(expectedApiKey);
    }
}
