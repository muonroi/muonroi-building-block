using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Experience.Abstractions;

namespace Muonroi.Experience.Runtime.Internal;

/// <summary>
/// Estimates token usage and enforces per-tier token budgets for experience entries.
/// Token estimation: sum of all text field character lengths divided by 4.
/// </summary>
internal static class TokenBudgetEnforcer
{
    /// <summary>
    /// Estimates the token count for a single experience entry.
    /// Uses the heuristic: total character length of all text fields / 4.
    /// Fields counted: Trigger, Question, Solution, Principle (if set), Reasoning (all items).
    /// </summary>
    public static int EstimateTokens(NeuronExperience e)
        => (e.Trigger.Length + e.Question.Length + e.Solution.Length
            + (e.Principle?.Length ?? 0)
            + e.Reasoning.Sum(r => r.Length)) / 4;

    /// <summary>
    /// Returns true if adding the candidate to the existing collection would exceed the budget.
    /// A result of false means the candidate fits (including exact boundary — used == budget is not exceeded).
    /// </summary>
    public static bool WouldExceedBudget(
        IEnumerable<NeuronExperience> existing,
        NeuronExperience candidate,
        int budget)
    {
        int used = existing.Sum(EstimateTokens);
        return used + EstimateTokens(candidate) > budget;
    }

    /// <summary>
    /// Returns the token budget for the given tier from the supplied config.
    /// Tier 3 (RawTrajectory) returns int.MaxValue — effectively no limit.
    /// </summary>
    public static int GetBudgetForTier(ExperienceTier tier, ExperienceBudgetConfig config) => tier switch
    {
        ExperienceTier.Principle => config.PrincipleBudget,
        ExperienceTier.Behavioral => config.BehavioralBudget,
        ExperienceTier.SelfQA => config.SelfQABudget,
        ExperienceTier.RawTrajectory => int.MaxValue,
        _ => MGuard.Fail<int>($"Unknown ExperienceTier: {tier}")
    };
}
