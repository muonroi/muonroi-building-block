using Microsoft.Extensions.Caching.Memory;
using Muonroi.Rules;

namespace Muonroi.BuildingBlock.Test;

public class BusinessRuleExtensionsTests
{
    private sealed class CountingRule : IBusinessRule<int>
    {
        public int Count { get; private set; }
        public string Code => "COUNT";

        public Task<bool> IsSatisfiedAsync(int context, CancellationToken cancellationToken = default)
        {
            Count++;
            return Task.FromResult(context > 0);
        }
    }

    [Fact]
    public async Task Cache_Memoizes_Result()
    {
        CountingRule inner = new();
        MemoryCache cache = new(new MemoryCacheOptions());
        IBusinessRule<int> rule = inner.Cache(cache);

        bool first = await rule.IsSatisfiedAsync(1);
        bool second = await rule.IsSatisfiedAsync(1);

        Assert.True(first);
        Assert.True(second);
        Assert.Equal(1, inner.Count);
    }

    [Fact]
    public async Task Adapt_Maps_Context()
    {
        IBusinessRule<int> positive = new CountingRule();
        IBusinessRule<string> adapted = positive.Adapt<string, int>(s => int.Parse(s));

        bool result = await adapted.IsSatisfiedAsync("5");

        Assert.True(result);
    }
}
