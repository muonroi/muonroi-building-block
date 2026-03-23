namespace Muonroi.Rules.Tests.Rules;

public class BusinessRuleExtensionsTests
{
    private class TrueRule : IBusinessRule<string>
    {
        public string Code => "TRUE";
        public Task<bool> IsSatisfiedAsync(string context, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private class FalseRule : IBusinessRule<string>
    {
        public string Code => "FALSE";
        public Task<bool> IsSatisfiedAsync(string context, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    [Fact]
    public async Task And_BothTrue_ShouldReturnTrue()
    {
        var combined = new TrueRule().And(new TrueRule());
        (await combined.IsSatisfiedAsync("ctx")).Should().BeTrue();
    }

    [Fact]
    public async Task And_OneFalse_ShouldReturnFalse()
    {
        var combined = new TrueRule().And(new FalseRule());
        (await combined.IsSatisfiedAsync("ctx")).Should().BeFalse();
    }

    [Fact]
    public async Task And_BothFalse_ShouldReturnFalse()
    {
        var combined = new FalseRule().And(new FalseRule());
        (await combined.IsSatisfiedAsync("ctx")).Should().BeFalse();
    }

    [Fact]
    public async Task Or_BothTrue_ShouldReturnTrue()
    {
        var combined = new TrueRule().Or(new TrueRule());
        (await combined.IsSatisfiedAsync("ctx")).Should().BeTrue();
    }

    [Fact]
    public async Task Or_OneFalse_ShouldReturnTrue()
    {
        var combined = new TrueRule().Or(new FalseRule());
        (await combined.IsSatisfiedAsync("ctx")).Should().BeTrue();
    }

    [Fact]
    public async Task Or_BothFalse_ShouldReturnFalse()
    {
        var combined = new FalseRule().Or(new FalseRule());
        (await combined.IsSatisfiedAsync("ctx")).Should().BeFalse();
    }

    [Fact]
    public void And_Code_ShouldCombineCodes()
    {
        var combined = new TrueRule().And(new FalseRule());
        combined.Code.Should().Be("TRUE AND FALSE");
    }

    [Fact]
    public void Or_Code_ShouldCombineCodes()
    {
        var combined = new TrueRule().Or(new FalseRule());
        combined.Code.Should().Be("TRUE OR FALSE");
    }

    [Fact]
    public async Task Cache_ShouldReturnCachedResult()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        int callCount = 0;
        var rule = Substitute.For<IBusinessRule<string>>();
        rule.Code.Returns("CACHED");
        rule.IsSatisfiedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callCount++; return Task.FromResult(true); });

        var cached = rule.Cache(cache);

        await cached.IsSatisfiedAsync("test");
        await cached.IsSatisfiedAsync("test");

        callCount.Should().Be(1);
        cached.Code.Should().Be("CACHED");
    }

    [Fact]
    public async Task Cache_DifferentContexts_ShouldEvaluateSeparately()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        int callCount = 0;
        var rule = Substitute.For<IBusinessRule<int>>();
        rule.Code.Returns("MULTI");
        rule.IsSatisfiedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callCount++; return Task.FromResult(true); });

        var cached = rule.Cache(cache);

        await cached.IsSatisfiedAsync(1);
        await cached.IsSatisfiedAsync(2);

        callCount.Should().Be(2);
    }

    [Fact]
    public async Task Adapt_ShouldMapContextBeforeEvaluation()
    {
        var inner = Substitute.For<IBusinessRule<int>>();
        inner.Code.Returns("INNER");
        inner.IsSatisfiedAsync(42, Arg.Any<CancellationToken>()).Returns(true);

        var adapted = inner.Adapt<string, int>(s => s.Length);

        // "The answer to everything has 42 ch" — well, let's use a string of length 42
        var result = await adapted.IsSatisfiedAsync(new string('x', 42));

        result.Should().BeTrue();
        adapted.Code.Should().Be("INNER");
    }

    [Fact]
    public async Task ChainedComposition_ShouldWork()
    {
        var combined = new TrueRule().And(new TrueRule()).Or(new FalseRule());
        (await combined.IsSatisfiedAsync("ctx")).Should().BeTrue();
    }
}
