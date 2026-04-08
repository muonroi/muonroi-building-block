namespace Muonroi.Experience.Abstractions;

/// <summary>Storage tier for a NeuronExperience entry.</summary>
public enum ExperienceTier
{
    /// <summary>Generalized principles — highest confidence, lowest cardinality (~400 token budget).</summary>
    Principle = 0,
    /// <summary>Behavioral rules — confirmed patterns, promoted from Self-QA (~600 token budget).</summary>
    Behavioral = 1,
    /// <summary>Self-QA cache — structured Q→Why→Solution, promoted after 3 confirmed hits (~500 token budget).</summary>
    SelfQA = 2,
    /// <summary>Raw trajectories — unprocessed session logs, high cardinality, lowest priority.</summary>
    RawTrajectory = 3
}
