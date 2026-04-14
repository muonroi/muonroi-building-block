namespace Muonroi.Experience.Abstractions;

/// <summary>A search hit returned by IExperienceStore.FindRelevantAsync.</summary>
public sealed record ExperienceSearchResult(
    NeuronExperience Experience,
    float RelevanceScore);
