using FluentAssertions;
using Muonroi.Experience.Abstractions;
using Muonroi.Experience.Runtime.Internal;
using Xunit;

namespace Muonroi.Experience.Runtime.Tests;

public class TokenBudgetEnforcerTests
{
    // Helper: creates a NeuronExperience with controlled text for token estimation.
    private static NeuronExperience MakeExperience(
        string trigger,
        string question,
        string solution,
        string[]? reasoning = null,
        string? principle = null)
        => new()
        {
            Id = Guid.NewGuid().ToString(),
            Trigger = trigger,
            Question = question,
            Solution = solution,
            Reasoning = reasoning ?? [],
            Principle = principle,
            CreatedFrom = "test-session",
            Tier = ExperienceTier.SelfQA,
            CreatedAt = DateTimeOffset.UtcNow
        };

    [Fact]
    public void EstimateTokens_CalculatesFromAllTextFields()
    {
        // Trigger=4, Question=4, Solution=3, Reasoning=["because"]=7 => (4+4+3+0+7)/4 = 18/4 = 4
        var entry = MakeExperience("test", "what", "fix", ["because"]);

        int tokens = TokenBudgetEnforcer.EstimateTokens(entry);

        tokens.Should().Be(4);
    }

    [Fact]
    public void EstimateTokens_IncludesPrincipleWhenSet()
    {
        // Trigger=4, Question=4, Solution=3, Principle="always read first"=18, Reasoning=[] => (4+4+3+18+0)/4 = 29/4 = 7
        var entry = MakeExperience("test", "what", "fix", principle: "always read first");

        int tokens = TokenBudgetEnforcer.EstimateTokens(entry);

        tokens.Should().Be(7);
    }

    [Fact]
    public void WouldExceedBudget_EmptyExisting_UnderBudget_ReturnsFalse()
    {
        // candidate tokens = (4+4+3+0+7)/4 = 4, budget = 20 => 0 + 4 <= 20 => false
        var candidate = MakeExperience("test", "what", "fix", ["because"]);

        bool result = TokenBudgetEnforcer.WouldExceedBudget([], candidate, budget: 20);

        result.Should().BeFalse();
    }

    [Fact]
    public void WouldExceedBudget_ExistingPlusCandidateExceedsBudget_ReturnsTrue()
    {
        // We need existing to use 15 tokens and candidate 10 tokens => 25 > 20 => true
        // 15 tokens => need chars = 15*4 = 60 total; use Trigger=20, Question=20, Solution=20, Reasoning=[]
        var existing = MakeExperience(
            new string('a', 20),
            new string('b', 20),
            new string('c', 20));

        // existing tokens = (20+20+20+0+0)/4 = 60/4 = 15

        // 10 tokens => need chars = 40; use Trigger=14, Question=13, Solution=13
        var candidate = MakeExperience(
            new string('x', 14),
            new string('y', 13),
            new string('z', 13));

        // candidate tokens = (14+13+13+0+0)/4 = 40/4 = 10

        bool result = TokenBudgetEnforcer.WouldExceedBudget([existing], candidate, budget: 20);

        result.Should().BeTrue("15 + 10 = 25 > 20");
    }

    [Fact]
    public void WouldExceedBudget_ExactBoundary_ReturnsFalse()
    {
        // existing=15 tokens, candidate=5 tokens => 20 == budget => not exceeded => false
        var existing = MakeExperience(
            new string('a', 20),
            new string('b', 20),
            new string('c', 20));

        // existing tokens = (20+20+20)/4 = 15

        // candidate = 5 tokens => need 20 chars; Trigger=7, Question=7, Solution=6
        var candidate = MakeExperience(
            new string('x', 7),
            new string('y', 7),
            new string('z', 6));

        // candidate tokens = (7+7+6)/4 = 20/4 = 5

        bool result = TokenBudgetEnforcer.WouldExceedBudget([existing], candidate, budget: 20);

        result.Should().BeFalse("15 + 5 = 20 == budget, not exceeded");
    }

    [Fact]
    public void GetBudgetForTier_ReturnsCorrectBudgets()
    {
        var config = new ExperienceBudgetConfig();

        TokenBudgetEnforcer.GetBudgetForTier(ExperienceTier.Principle, config).Should().Be(400);
        TokenBudgetEnforcer.GetBudgetForTier(ExperienceTier.Behavioral, config).Should().Be(600);
        TokenBudgetEnforcer.GetBudgetForTier(ExperienceTier.SelfQA, config).Should().Be(500);
        TokenBudgetEnforcer.GetBudgetForTier(ExperienceTier.RawTrajectory, config).Should().Be(int.MaxValue);
    }

    [Fact]
    public void GetBudgetForTier_RawTrajectory_ReturnsIntMaxValue()
    {
        var config = new ExperienceBudgetConfig();

        int budget = TokenBudgetEnforcer.GetBudgetForTier(ExperienceTier.RawTrajectory, config);

        budget.Should().Be(int.MaxValue, "Tier 3 (RawTrajectory) has no budget limit");
    }
}
