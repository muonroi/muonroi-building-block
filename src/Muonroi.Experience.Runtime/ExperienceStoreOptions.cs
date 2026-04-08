using Muonroi.Experience.Abstractions;

namespace Muonroi.Experience.Runtime;

/// <summary>
/// Configuration options for the experience store, used in DI registration.
/// Bind from appsettings.json section "ExperienceStore".
/// </summary>
public sealed class ExperienceStoreOptions
{
    /// <summary>Which backing store implementation to use. Default: File.</summary>
    public ExperienceStoreType StoreType { get; set; } = ExperienceStoreType.File;

    /// <summary>Qdrant gRPC endpoint. Default: http://localhost:6334.</summary>
    public string QdrantUrl { get; set; } = "http://localhost:6334";

    /// <summary>Directory path for the file-based store. Default: ./experience-store.</summary>
    public string FileDirectoryPath { get; set; } = "./experience-store";

    /// <summary>
    /// Embedding vector size. Callers MUST set this to match their embedding model output dimension.
    /// QdrantExperienceStore validates > 0 at startup.
    /// </summary>
    public int VectorSize { get; set; }

    /// <summary>Token budget and dedup thresholds. Defaults match ExperienceBudgetConfig.</summary>
    public ExperienceBudgetConfig Budget { get; set; } = new();
}
