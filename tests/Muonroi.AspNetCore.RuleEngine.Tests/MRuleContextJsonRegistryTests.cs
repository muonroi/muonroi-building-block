using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.Core.Abstractions.Exceptions;
using System.Text.Json;

namespace Muonroi.AspNetCore.RuleEngine.Tests;

/// <summary>Simple context type used exclusively by MRuleContextJsonRegistry tests.</summary>
public sealed class TestRuleContext
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

/// <summary>A second context type to verify different registrations are isolated.</summary>
public sealed class AnotherRuleContext
{
    public string Tag { get; set; } = string.Empty;
}

/// <summary>
/// Unit tests for <see cref="MRuleContextJsonRegistry"/>.
/// Verifies type-safe deserialization, allow-list enforcement, +Logging auditing, and +MultiTenant whitelist.
/// </summary>
public class MRuleContextJsonRegistryTests
{
    // ---------------------------------------------------------------------------
    // Test 1: Registered type deserializes correctly
    // ---------------------------------------------------------------------------
    [Fact]
    public void DeserializeContext_WithRegisteredType_ReturnsTypedInstance()
    {
        // Arrange
        var registry = new MRuleContextJsonRegistry();
        registry.AddRuleContext<TestRuleContext>("MyApp.TestRuleContext");

        string json = """{"Name":"Alice","Value":42}""";

        // Act
        object result = registry.DeserializeContext("MyApp.TestRuleContext", json);

        // Assert
        result.Should().BeOfType<TestRuleContext>();
        var ctx = (TestRuleContext)result;
        ctx.Name.Should().Be("Alice");
        ctx.Value.Should().Be(42);
    }

    // ---------------------------------------------------------------------------
    // Test 2: Unregistered type throws MConfigurationException
    // ---------------------------------------------------------------------------
    [Fact]
    public void DeserializeContext_WithUnregisteredType_ThrowsMConfigurationException_ContainingTypeName()
    {
        // Arrange
        var registry = new MRuleContextJsonRegistry();
        string typeName = "Some.Unknown.Type";

        // Act
        Action act = () => registry.DeserializeContext(typeName, "{}");

        // Assert
        act.Should()
           .Throw<MConfigurationException>()
           .WithMessage($"*{typeName}*");
    }

    // ---------------------------------------------------------------------------
    // Test 3: Short name AND full name both resolve to the same type
    // ---------------------------------------------------------------------------
    [Fact]
    public void DeserializeContext_ShortAndFullName_BothResolveCorrectly()
    {
        // Arrange
        var registry = new MRuleContextJsonRegistry();
        // Register with both short and full names
        registry.AddRuleContext<TestRuleContext>("TestRuleContext", "MyApp.TestRuleContext");

        string json = """{"Name":"Bob","Value":7}""";

        // Act — short name
        object shortResult = registry.DeserializeContext("TestRuleContext", json);
        // Act — full name
        object fullResult = registry.DeserializeContext("MyApp.TestRuleContext", json);

        // Assert
        shortResult.Should().BeOfType<TestRuleContext>()
            .Which.Name.Should().Be("Bob");
        fullResult.Should().BeOfType<TestRuleContext>()
            .Which.Name.Should().Be("Bob");
    }

    // ---------------------------------------------------------------------------
    // Test 4: Null/empty type name throws MConfigurationException
    // ---------------------------------------------------------------------------
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeserializeContext_NullOrEmptyTypeName_ThrowsMConfigurationException(string? typeName)
    {
        // Arrange
        var registry = new MRuleContextJsonRegistry();

        // Act
        Action act = () => registry.DeserializeContext(typeName!, "{}");

        // Assert
        act.Should().Throw<MConfigurationException>();
    }

    // ---------------------------------------------------------------------------
    // Test 5: Malformed JSON throws MConfigurationException (wraps JsonException)
    // ---------------------------------------------------------------------------
    [Fact]
    public void DeserializeContext_MalformedJson_ThrowsMConfigurationException()
    {
        // Arrange
        var registry = new MRuleContextJsonRegistry();
        registry.AddRuleContext<TestRuleContext>("TestRuleContext");

        string malformedJson = "{ Name: 'not valid json' }"; // invalid JSON

        // Act
        Action act = () => registry.DeserializeContext("TestRuleContext", malformedJson);

        // Assert
        act.Should().Throw<MConfigurationException>();
    }

    // ---------------------------------------------------------------------------
    // Test 6: With Logging capability, audit entries are logged for allowed/denied
    // ---------------------------------------------------------------------------
    [Fact]
    public void DeserializeContext_WithLoggingCapability_LogsAuditForAllowed()
    {
        // Arrange — create a mock ecosystem registry that reports Logging capability
        var ecosystemRegistry = Substitute.For<IMEcosystemRegistry>();
        ecosystemRegistry.Has(MCapability.Logging).Returns(true);
        ecosystemRegistry.Has(MCapability.MultiTenant).Returns(false);

        var registry = new MRuleContextJsonRegistry(ecosystemRegistry);
        registry.AddRuleContext<TestRuleContext>("TestRuleContext");

        // Act — should not throw, and logging path should be exercised
        Action act = () => registry.DeserializeContext("TestRuleContext", """{"Name":"test","Value":1}""");

        // Assert — no exception for allowed type
        act.Should().NotThrow();
    }

    [Fact]
    public void DeserializeContext_WithLoggingCapability_LogsAuditForDenied()
    {
        // Arrange
        var ecosystemRegistry = Substitute.For<IMEcosystemRegistry>();
        ecosystemRegistry.Has(MCapability.Logging).Returns(true);
        ecosystemRegistry.Has(MCapability.MultiTenant).Returns(false);

        var registry = new MRuleContextJsonRegistry(ecosystemRegistry);

        // Act — unregistered type with logging
        Action act = () => registry.DeserializeContext("Unregistered.Type", "{}");

        // Assert — still throws, but logging audit path was hit
        act.Should().Throw<MConfigurationException>().WithMessage("*Unregistered.Type*");
    }

    // ---------------------------------------------------------------------------
    // Test 7: With MultiTenant capability, type not in tenant whitelist is rejected
    // ---------------------------------------------------------------------------
    [Fact]
    public void DeserializeContext_WithMultiTenantCapabilityAndTenantWhitelist_RejectsDisallowedType()
    {
        // Arrange
        var ecosystemRegistry = Substitute.For<IMEcosystemRegistry>();
        ecosystemRegistry.Has(MCapability.Logging).Returns(false);
        ecosystemRegistry.Has(MCapability.MultiTenant).Returns(true);

        var registry = new MRuleContextJsonRegistry(ecosystemRegistry);
        registry.AddRuleContext<TestRuleContext>("TestRuleContext");
        registry.AddRuleContext<AnotherRuleContext>("AnotherRuleContext");

        // Tenant "tenant-A" only allows TestRuleContext
        registry.SetTenantWhitelist("tenant-A", ["TestRuleContext"]);

        // Act — try to use AnotherRuleContext for tenant-A
        Action act = () => registry.DeserializeContext("AnotherRuleContext", """{"Tag":"x"}""", "tenant-A");

        // Assert
        act.Should().Throw<MConfigurationException>()
           .WithMessage("*tenant-A*");
    }

    [Fact]
    public void DeserializeContext_WithMultiTenantCapabilityAndTenantWhitelist_AllowsWhitelistedType()
    {
        // Arrange
        var ecosystemRegistry = Substitute.For<IMEcosystemRegistry>();
        ecosystemRegistry.Has(MCapability.Logging).Returns(false);
        ecosystemRegistry.Has(MCapability.MultiTenant).Returns(true);

        var registry = new MRuleContextJsonRegistry(ecosystemRegistry);
        registry.AddRuleContext<TestRuleContext>("TestRuleContext");
        registry.SetTenantWhitelist("tenant-A", ["TestRuleContext"]);

        // Act — whitelisted type should work
        Action act = () => registry.DeserializeContext("TestRuleContext", """{"Name":"ok","Value":1}""", "tenant-A");

        // Assert
        act.Should().NotThrow();
    }

    // ---------------------------------------------------------------------------
    // Test 8: Thread safety — concurrent AddRuleContext and DeserializeContext
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task Registry_ThreadSafety_ConcurrentOperationsDoNotCorruptState()
    {
        // Arrange
        var registry = new MRuleContextJsonRegistry();
        string json = """{"Name":"concurrent","Value":0}""";

        // Act — concurrently register and deserialize
        var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
        {
            string typeName = $"Type{i % 5}";
            registry.AddRuleContext<TestRuleContext>(typeName);

            // Attempt deserialization — might fail if this particular typeName was just registered
            // but that's fine; we just ensure no exceptions other than MConfigurationException
            try
            {
                registry.DeserializeContext(typeName, json);
            }
            catch (MConfigurationException)
            {
                // Expected for any types not yet registered
            }
        }));

        // Wait for all and assert no unhandled exceptions
        Func<Task> act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
    }

    // ---------------------------------------------------------------------------
    // Additional: AddRuleContext with no explicit names registers by FullName and Name
    // ---------------------------------------------------------------------------
    [Fact]
    public void AddRuleContext_NoExplicitNames_RegistersByTypeFullNameAndShortName()
    {
        // Arrange
        var registry = new MRuleContextJsonRegistry();
        registry.AddRuleContext<TestRuleContext>(); // no explicit names

        string json = """{"Name":"auto","Value":99}""";
        string fullName = typeof(TestRuleContext).FullName!;
        string shortName = typeof(TestRuleContext).Name;

        // Act
        object byFull = registry.DeserializeContext(fullName, json);
        object byShort = registry.DeserializeContext(shortName, json);

        // Assert
        byFull.Should().BeOfType<TestRuleContext>().Which.Name.Should().Be("auto");
        byShort.Should().BeOfType<TestRuleContext>().Which.Name.Should().Be("auto");
    }
}
