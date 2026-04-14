namespace Muonroi.Experience.Runtime;

/// <summary>Specifies the backing store implementation for the experience engine.</summary>
public enum ExperienceStoreType
{
    /// <summary>Qdrant vector database — recommended for semantic search.</summary>
    Qdrant = 0,

    /// <summary>Local file system — suitable for development and single-node deployments.</summary>
    File = 1
}
