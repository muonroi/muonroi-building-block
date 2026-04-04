using Muonroi.Core.Abstractions.Ecosystem;

namespace Muonroi.Core.Abstractions.Tests.Ecosystem;

public class MEcosystemRegistryTests
{
    private static MEcosystemRegistry CreateRegistry() => new();

    [Fact]
    public void NewRegistry_Active_IsNone()
    {
        var registry = CreateRegistry();
        registry.Active.Should().Be(MCapability.None);
    }

    [Fact]
    public void Register_Logging_Has_Logging_ReturnsTrue()
    {
        var registry = CreateRegistry();
        registry.Register(MCapability.Logging);
        registry.Has(MCapability.Logging).Should().BeTrue();
    }

    [Fact]
    public void Register_Logging_Has_Auth_ReturnsFalse()
    {
        var registry = CreateRegistry();
        registry.Register(MCapability.Logging);
        registry.Has(MCapability.Auth).Should().BeFalse();
    }

    [Fact]
    public void Register_Logging_Then_Auth_Active_EqualsBoth()
    {
        var registry = CreateRegistry();
        registry.Register(MCapability.Logging);
        registry.Register(MCapability.Auth);
        registry.Active.Should().Be(MCapability.Logging | MCapability.Auth);
    }

    [Fact]
    public void Register_Logging_Twice_IsIdempotent()
    {
        var registry = CreateRegistry();
        registry.Register(MCapability.Logging);
        registry.Register(MCapability.Logging);
        registry.Active.Should().Be(MCapability.Logging);
    }

    [Fact]
    public void Has_CombinedFlag_ReturnsTrueOnlyWhenBothRegistered()
    {
        var registry = CreateRegistry();
        registry.Register(MCapability.Logging);
        registry.Has(MCapability.Logging | MCapability.Auth).Should().BeFalse();

        registry.Register(MCapability.Auth);
        registry.Has(MCapability.Logging | MCapability.Auth).Should().BeTrue();
    }

    [Fact]
    public void Has_None_AlwaysReturnsTrue()
    {
        // Vacuous truth: (None & None) == None is always true
        var registry = CreateRegistry();
        registry.Has(MCapability.None).Should().BeTrue();
    }

    [Fact]
    public void Register_MultipleFlagsAtOnce_ActiveContainsAll()
    {
        var registry = CreateRegistry();
        registry.Register(MCapability.Logging | MCapability.RuleEngine | MCapability.MultiTenant);
        registry.Has(MCapability.Logging).Should().BeTrue();
        registry.Has(MCapability.RuleEngine).Should().BeTrue();
        registry.Has(MCapability.MultiTenant).Should().BeTrue();
        registry.Has(MCapability.Auth).Should().BeFalse();
    }
}
