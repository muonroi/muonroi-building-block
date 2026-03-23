using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class BusinessRuleExtensionsTests
{
    [Fact]
    public async Task And_BothTrue_ReturnsTrue()
    {
        IBusinessRule<string> left = new ConstantRule("L", true);
        IBusinessRule<string> right = new ConstantRule("R", true);

        IBusinessRule<string> combined = left.And(right);

        bool result = await combined.IsSatisfiedAsync("ctx");
        result.Should().BeTrue();
        combined.Code.Should().Contain("AND");
    }

    [Fact]
    public async Task And_OneFalse_ReturnsFalse()
    {
        IBusinessRule<string> left = new ConstantRule("L", true);
        IBusinessRule<string> right = new ConstantRule("R", false);

        bool result = await left.And(right).IsSatisfiedAsync("ctx");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Or_OneFalse_ReturnsTrue()
    {
        IBusinessRule<string> left = new ConstantRule("L", false);
        IBusinessRule<string> right = new ConstantRule("R", true);

        bool result = await left.Or(right).IsSatisfiedAsync("ctx");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Or_BothFalse_ReturnsFalse()
    {
        IBusinessRule<string> left = new ConstantRule("L", false);
        IBusinessRule<string> right = new ConstantRule("R", false);

        bool result = await left.Or(right).IsSatisfiedAsync("ctx");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Cache_ReturnsCachedResult()
    {
        using IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        CountingRule inner = new("counting", true);
        IBusinessRule<string> cached = inner.Cache(cache, TimeSpan.FromMinutes(5));

        await cached.IsSatisfiedAsync("ctx");
        await cached.IsSatisfiedAsync("ctx");

        inner.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Cache_DifferentContexts_EvaluatesSeparately()
    {
        using IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        CountingRule inner = new("counting", true);
        IBusinessRule<string> cached = inner.Cache(cache, TimeSpan.FromMinutes(5));

        await cached.IsSatisfiedAsync("ctx-1");
        await cached.IsSatisfiedAsync("ctx-2");

        inner.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Adapt_TransformsContext()
    {
        IBusinessRule<int> innerRule = new IntRule("int-rule");
        IBusinessRule<string> adapted = innerRule.Adapt<string, int>(s => s.Length);

        bool result = await adapted.IsSatisfiedAsync("hello");

        result.Should().BeTrue(); // "hello".Length = 5 > 0
    }

    [Fact]
    public async Task ComplexComposition_AndOrChain()
    {
        IBusinessRule<string> rule = new ConstantRule("A", true)
            .And(new ConstantRule("B", true))
            .Or(new ConstantRule("C", false));

        bool result = await rule.IsSatisfiedAsync("ctx");

        result.Should().BeTrue();
    }

    // --- Helpers ---

    private sealed class ConstantRule(string code, bool result) : IBusinessRule<string>
    {
        public string Code => code;
        public Task<bool> IsSatisfiedAsync(string context, CancellationToken ct = default)
            => Task.FromResult(result);
    }

    private sealed class CountingRule(string code, bool result) : IBusinessRule<string>
    {
        public string Code => code;
        public int CallCount { get; private set; }
        public Task<bool> IsSatisfiedAsync(string context, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class IntRule(string code) : IBusinessRule<int>
    {
        public string Code => code;
        public Task<bool> IsSatisfiedAsync(int context, CancellationToken ct = default)
            => Task.FromResult(context > 0);
    }
}
