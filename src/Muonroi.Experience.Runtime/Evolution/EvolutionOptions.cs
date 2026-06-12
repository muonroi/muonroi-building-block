namespace Muonroi.Experience.Runtime.Evolution;

/// <summary>
/// Configuration for the <see cref="ExperienceEvolutionOrchestrator"/> and
/// <see cref="EvolutionBackgroundService"/>.
/// </summary>
public sealed class EvolutionOptions
{
    /// <summary>
    /// Jaccard similarity threshold above which two Tier 2 entries are placed in the same cluster.
    /// Default: 0.7 (70% word overlap on trigger + question tokens).
    /// </summary>
    public float ClusterSimilarityThreshold { get; set; } = 0.7f;

    /// <summary>
    /// Minimum number of entries in a cluster before the cluster is abstracted into a principle.
    /// Default: 3.
    /// </summary>
    public int MinClusterSize { get; set; } = 3;

    /// <summary>
    /// When <c>true</c>, registers <see cref="EvolutionBackgroundService"/> as an
    /// <see cref="Microsoft.Extensions.Hosting.IHostedService"/> so evolution runs weekly automatically.
    /// Default: <c>false</c> (opt-in — invoke <see cref="ExperienceEvolutionOrchestrator.RunEvolutionAsync"/> manually).
    /// </summary>
    public bool EnableBackgroundService { get; set; } = false;
}
