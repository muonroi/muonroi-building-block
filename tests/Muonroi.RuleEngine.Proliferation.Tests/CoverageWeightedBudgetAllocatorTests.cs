using FluentAssertions;
using Muonroi.RuleEngine.Proliferation.Brain;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class CoverageWeightedBudgetAllocatorTests
{
    private readonly CoverageWeightedBudgetAllocator _allocator = new();

    [Fact]
    public void AllocateBudget_LowCoverage_Returns3xBaseBudget()
    {
        // < 30% coverage → 3x multiplier, no failure bonus
        int result = _allocator.AllocateBudget("workflow-a", coveragePercent: 20, failureRate: 0, baseBudget: 10);

        result.Should().Be(30); // 10 * 3.0 * 1.0 = 30
    }

    [Fact]
    public void AllocateBudget_MediumCoverage_Returns2xBaseBudget()
    {
        // 30-60% coverage → 2x multiplier
        int result = _allocator.AllocateBudget("workflow-b", coveragePercent: 45, failureRate: 0, baseBudget: 10);

        result.Should().Be(20); // 10 * 2.0 * 1.0 = 20
    }

    [Fact]
    public void AllocateBudget_HighCoverage_ReturnsHalfBaseBudget()
    {
        // >= 80% coverage → 0.5x multiplier
        int result = _allocator.AllocateBudget("workflow-c", coveragePercent: 90, failureRate: 0, baseBudget: 10);

        result.Should().Be(5); // 10 * 0.5 * 1.0 = 5
    }

    [Fact]
    public void AllocateBudget_HighFailureRate_AppliesFailureBonus()
    {
        // < 60% coverage (2x) + > 30% failure rate (1.5x bonus) = 3x total
        int result = _allocator.AllocateBudget("workflow-d", coveragePercent: 50, failureRate: 40, baseBudget: 10);

        result.Should().Be(30); // 10 * 2.0 * 1.5 = 30
    }

    [Fact]
    public void AllocateBudget_NeverReturnsZeroOrLess()
    {
        // Even with 0 base budget, result should be >= 1
        int result = _allocator.AllocateBudget("workflow-e", coveragePercent: 95, failureRate: 0, baseBudget: 0);

        result.Should().BeGreaterThanOrEqualTo(1);
    }

    [Theory]
    [InlineData(25, 5, 10, 30)]   // < 30% coverage, low failure → 3x
    [InlineData(55, 5, 10, 20)]   // < 60% coverage, low failure → 2x
    [InlineData(70, 5, 10, 10)]   // < 80% coverage, low failure → 1x
    [InlineData(85, 5, 10, 5)]    // >= 80% coverage, low failure → 0.5x (rounded up)
    [InlineData(20, 35, 10, 45)]  // < 30% coverage + high failure → 3x * 1.5x
    public void AllocateBudget_AllTiers_ReturnsExpected(double coverage, double failureRate, int baseBudget, int expected)
    {
        int result = _allocator.AllocateBudget("test", coverage, failureRate, baseBudget);

        result.Should().Be(expected);
    }
}
