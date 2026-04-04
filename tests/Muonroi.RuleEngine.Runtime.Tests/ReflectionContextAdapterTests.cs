using FluentAssertions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Abstractions.Adapters;
using System.Reflection;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class ReflectionContextAdapterTests
{
    [Fact]
    public void ReflectionContextProjector_ShouldHandleNullAndDictionaryInputs()
    {
        object projector = CreateClosedGeneric("Muonroi.RuleEngine.Runtime.Adapters.ReflectionContextProjector`1", typeof(Dictionary<string, object?>));

        IReadOnlyDictionary<string, object?> fromNull = InvokeProjector<Dictionary<string, object?>>(projector, null!);
        IReadOnlyDictionary<string, object?> fromDict = InvokeProjector(projector, new Dictionary<string, object?> { ["amount"] = 12, ["tier"] = "vip" });

        fromNull.Should().BeEmpty();
        fromDict["amount"].Should().Be(12);
        fromDict["tier"].Should().Be("vip");
    }

    [Fact]
    public void ReflectionContextProjector_ShouldReadPublicProperties_AndSkipThrowingGetter()
    {
        object projector = CreateClosedGeneric("Muonroi.RuleEngine.Runtime.Adapters.ReflectionContextProjector`1", typeof(ProjectorContext));

        IReadOnlyDictionary<string, object?> result = InvokeProjector(projector, new ProjectorContext());

        result["Amount"].Should().Be(42);
        result["Tier"].Should().Be("gold");
        result.ContainsKey("Danger").Should().BeFalse();
    }

    [Fact]
    public void ReflectionContextFactory_ShouldPopulateProperties_UsingExactAndCamelCaseKeys()
    {
        object factory = CreateClosedGeneric("Muonroi.RuleEngine.Runtime.Adapters.ReflectionContextFactory`1", typeof(FactoryContext));
        FactBag facts = new();
        facts.Set("Amount", "12.5");
        facts.Set("customerName", "alice");
        facts.Set("Enabled", "true");

        FactoryContext context = InvokeFactory<FactoryContext>(factory, facts);

        context.Amount.Should().Be(12.5m);
        context.CustomerName.Should().Be("alice");
        context.Enabled.Should().BeTrue();
    }

    [Fact]
    public void ReflectionContextFactory_ShouldSkipIncompatibleValues()
    {
        object factory = CreateClosedGeneric("Muonroi.RuleEngine.Runtime.Adapters.ReflectionContextFactory`1", typeof(FactoryContext));
        FactBag facts = new();
        facts.Set("Amount", "not-a-number");
        facts.Set("Enabled", "nope");

        FactoryContext context = InvokeFactory<FactoryContext>(factory, facts);

        context.Amount.Should().Be(0);
        context.Enabled.Should().BeFalse();
    }

    private static object CreateClosedGeneric(string typeName, Type genericArg)
    {
        Assembly assembly = typeof(RulesEngineService).Assembly;
        Type openGeneric = assembly.GetType(typeName, throwOnError: true)!;
        Type closed = openGeneric.MakeGenericType(genericArg);
        return Activator.CreateInstance(closed, nonPublic: true)!;
    }

    private static IReadOnlyDictionary<string, object?> InvokeProjector<TContext>(object projector, TContext context)
    {
        MethodInfo method = projector.GetType().GetMethod("Project", BindingFlags.Instance | BindingFlags.Public)!;
        return (IReadOnlyDictionary<string, object?>)method.Invoke(projector, [context])!;
    }

    private static TContext InvokeFactory<TContext>(object factory, FactBag facts)
    {
        MethodInfo method = factory.GetType().GetMethod("Create", BindingFlags.Instance | BindingFlags.Public)!;
        return (TContext)method.Invoke(factory, [facts])!;
    }

    private sealed class ProjectorContext
    {
        public int Amount => 42;
        public string Tier => "gold";
        public string Danger => throw new InvalidOperationException("boom");
    }

    private sealed class FactoryContext
    {
        public decimal Amount { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public bool Enabled { get; set; }
    }
}
