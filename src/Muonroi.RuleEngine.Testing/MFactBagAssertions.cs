using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.RuleEngine.Testing;

public static class MFactBagAssertions
{
    public static MFactBagAssertion MShould(this FactBag facts) => new(facts);
}

public static class FactBagAssertions
{
    public static MFactBagAssertion Should(this FactBag facts) => new(facts);
}

public sealed class MFactBagAssertion(FactBag facts)
{
    public MFactBagAssertion Contain(string key, object? expectedValue = null)
    {
        IReadOnlyDictionary<string, object?> map = facts.AsReadOnly();
        if (!map.TryGetValue(key, out object? actual))
        {
            MGuard.State(false, $"Expected fact '{key}' to exist.");
        }

        if (expectedValue is not null && !Equals(actual, expectedValue))
        {
            MGuard.State(false, 
                $"Expected fact '{key}' to equal '{expectedValue}', actual '{actual ?? "<null>"}'.");
        }

        return this;
    }

    public MFactBagAssertion NotContain(string key)
    {
        if (facts.AsReadOnly().ContainsKey(key))
        {
            MGuard.State(false, $"Expected fact '{key}' to not exist.");
        }

        return this;
    }
}
