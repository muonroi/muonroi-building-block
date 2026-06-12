using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.Logging;
using Xunit;

namespace Muonroi.BuildingBlock.IntegrationTests.Ecosystem;

/// <summary>
/// Integration tests verifying real DI wiring for IMEcosystemRegistry + logging capability + IPostConfigureOptions pattern.
/// These tests use real ServiceCollection, not WebApplicationFactory, to avoid pulling in heavy dependencies.
/// </summary>
public class EcosystemRegistrationIntegrationTests
{
    [Fact]
    public void AddMuonroiLogging_RegistersLoggingCapability()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddMuonroiLogging());

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IMEcosystemRegistry>();

        registry.Has(MCapability.Logging).Should().BeTrue();
        registry.Has(MCapability.Auth).Should().BeFalse();
    }

    [Fact]
    public void NoCapabilityRegistered_RegistryActive_IsNone()
    {
        var services = new ServiceCollection();
        services.GetOrCreateRegistry(); // ensure singleton is created without registering any capability

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IMEcosystemRegistry>();

        registry.Active.Should().Be(MCapability.None);
    }

    [Fact]
    public void ManualRegistration_MultipleCapabilities_AllActive()
    {
        var services = new ServiceCollection();
        var registry = services.GetOrCreateRegistry();
        registry.Register(MCapability.Logging);
        registry.Register(MCapability.RuleEngine);
        registry.Register(MCapability.MultiTenant);
        registry.Register(MCapability.Auth);

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IMEcosystemRegistry>();

        // The singleton resolved from DI must be the same instance whose capabilities were registered
        resolved.Active.Should().Be(MCapability.Logging | MCapability.RuleEngine | MCapability.MultiTenant | MCapability.Auth);
    }

    [Fact]
    public void IPostConfigureOptions_Enhancer_SetsAuditEnabled_WhenLoggingRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddMuonroiLogging());

        // Register IPostConfigureOptions that checks registry.Has(Logging) before enhancing options
        services.AddOptions<TestOptions>();
        services.AddSingleton<IPostConfigureOptions<TestOptions>>(sp =>
        {
            var reg = sp.GetRequiredService<IMEcosystemRegistry>();
            return new LoggingAuditEnhancer(reg);
        });

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<TestOptions>>().Value;

        options.AuditEnabled.Should().BeTrue("Logging capability is active, so AuditEnabled should be enhanced to true");
    }

    [Fact]
    public void IPostConfigureOptions_Enhancer_DoesNotSetAuditEnabled_WhenLoggingNotRegistered()
    {
        var services = new ServiceCollection();
        // Do NOT register AddMuonroiLogging

        services.AddOptions<TestOptions>();
        services.AddSingleton<IPostConfigureOptions<TestOptions>>(sp =>
        {
            var reg = sp.GetRequiredService<IMEcosystemRegistry>();
            return new LoggingAuditEnhancer(reg);
        });
        // Manually get or create registry so IMEcosystemRegistry is resolvable
        services.GetOrCreateRegistry();

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<TestOptions>>().Value;

        options.AuditEnabled.Should().BeFalse("Logging capability is NOT active, so AuditEnabled should remain false");
    }

    [Fact]
    public void AddMuonroiLogging_DoubleRegistration_IsIdempotent()
    {
        // Calling AddMuonroiLogging twice should not fail and capability should still be Logging only
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddMuonroiLogging();
            builder.AddMuonroiLogging();
        });

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<IMEcosystemRegistry>();

        registry.Has(MCapability.Logging).Should().BeTrue();
        registry.Has(MCapability.Auth).Should().BeFalse();
    }
}

/// <summary>
/// Sample options class for the IPostConfigureOptions enhancer pattern demonstration.
/// </summary>
public class TestOptions
{
    /// <summary>
    /// Set to true by the ecosystem enhancer when Logging capability is active.
    /// </summary>
    public bool AuditEnabled { get; set; }
}

/// <summary>
/// Demonstrates the IPostConfigureOptions enhancer convention:
/// conditionally enhances options based on IMEcosystemRegistry.Has().
/// </summary>
internal sealed class LoggingAuditEnhancer(IMEcosystemRegistry registry) : IPostConfigureOptions<TestOptions>
{
    public void PostConfigure(string? name, TestOptions options)
    {
        if (registry.Has(MCapability.Logging))
        {
            options.AuditEnabled = true;
        }
    }
}
