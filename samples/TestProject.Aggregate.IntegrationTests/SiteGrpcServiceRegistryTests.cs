using System.Reflection;
using TestProject.Aggregate.Sites.Bravo;

namespace TestProject.Aggregate.IntegrationTests;

/// <summary>
/// Verifies that the SiteGrpcServiceRegistry source generator produces a registry
/// containing the BravoGrpcService entry with the correct SiteId and Reason.
///
/// The source generator (SiteProfileRegistrationGenerator) scans for [SiteGrpcService] attributes
/// and emits SiteGrpcServiceRegistry.g.cs in the Muonroi.Tenancy.SiteProfile.Generated namespace
/// inside the assembly that references the SourceGenerators package (Sites.Bravo assembly).
///
/// Covers GRPC-03: Source generator discovers BravoGrpcService and produces registry.
/// </summary>
public sealed class SiteGrpcServiceRegistryTests
{
    // The generated registry lives in the Sites.Bravo assembly since that project
    // references Muonroi.Tenancy.SiteProfile.SourceGenerators and has [SiteGrpcService] types.
    private const string RegistryTypeName = "Muonroi.Tenancy.SiteProfile.Generated.SiteGrpcServiceRegistry";
    private const string GetAllMethodName = "GetAllSiteGrpcServices";

    /// <summary>
    /// Helper: invoke SiteGrpcServiceRegistry.GetAllSiteGrpcServices() via reflection
    /// from the Sites.Bravo assembly and return the descriptor list.
    /// </summary>
    private static System.Collections.IList GetDescriptors()
    {
        Assembly bravoAssembly = typeof(BravoGrpcService).Assembly;
        Type? registryType = bravoAssembly.GetType(RegistryTypeName);
        registryType.Should().NotBeNull(
            because: $"Source generator should emit '{RegistryTypeName}' in the Sites.Bravo assembly. " +
                     $"Check that Muonroi.Tenancy.SiteProfile.SourceGenerators is referenced in Sites.Bravo.csproj.");

        MethodInfo? method = registryType!.GetMethod(GetAllMethodName,
            BindingFlags.Public | BindingFlags.Static);
        method.Should().NotBeNull(
            because: $"'{RegistryTypeName}' should have a public static '{GetAllMethodName}()' method.");

        object? result = method!.Invoke(null, null);
        result.Should().BeAssignableTo<System.Collections.IEnumerable>(
            because: "GetAllSiteGrpcServices() should return an IEnumerable of descriptors.");

        return (System.Collections.IList)result!;
    }

    /// <summary>
    /// GRPC-03: The source-generated registry must contain an entry for BravoGrpcService
    /// with SiteId == "BRAVO".
    /// </summary>
    [Fact]
    public void Registry_ContainsBravoEntry()
    {
        // Act
        System.Collections.IList descriptors = GetDescriptors();

        // Assert: find a descriptor with SiteId == "BRAVO" and ServiceType == typeof(BravoGrpcService)
        bool hasBravoEntry = false;
        foreach (object? descriptor in descriptors)
        {
            if (descriptor is null) continue;
            Type descType = descriptor.GetType();
            string? siteId = descType.GetProperty("SiteId")?.GetValue(descriptor) as string;
            Type? serviceType = descType.GetProperty("ServiceType")?.GetValue(descriptor) as Type;
            if (siteId == "BRAVO" && serviceType == typeof(BravoGrpcService))
            {
                hasBravoEntry = true;
                break;
            }
        }

        hasBravoEntry.Should().BeTrue(
            because: "Source generator should have emitted an entry for BravoGrpcService with SiteId='BRAVO' " +
                     "because BravoGrpcService is decorated with [SiteGrpcService(\"BRAVO\")].");
    }

    /// <summary>
    /// GRPC-03: The Bravo registry entry must have the correct Reason from the attribute.
    /// </summary>
    [Fact]
    public void Registry_BravoEntry_HasCorrectReason()
    {
        // Act
        System.Collections.IList descriptors = GetDescriptors();

        // Assert: find the BRAVO descriptor and check its Reason
        string? bravoReason = null;
        foreach (object? descriptor in descriptors)
        {
            if (descriptor is null) continue;
            Type descType = descriptor.GetType();
            string? siteId = descType.GetProperty("SiteId")?.GetValue(descriptor) as string;
            if (siteId == "BRAVO")
            {
                bravoReason = descType.GetProperty("Reason")?.GetValue(descriptor) as string;
                break;
            }
        }

        bravoReason.Should().NotBeNullOrEmpty(
            because: "BravoGrpcService has Reason = \"Bravo has unique container validation RPCs\" in its [SiteGrpcService] attribute.");
        bravoReason.Should().Contain("unique container validation",
            because: "The Reason on [SiteGrpcService(\"BRAVO\", Reason = \"...\")] should be propagated to the generated registry.");
    }

    /// <summary>
    /// GRPC-03: The registry must be non-empty — at least one site gRPC service was discovered.
    /// </summary>
    [Fact]
    public void Registry_EntryCount_IsAtLeastOne()
    {
        // Act
        System.Collections.IList descriptors = GetDescriptors();

        // Assert
        descriptors.Count.Should().BeGreaterThanOrEqualTo(1,
            because: "The Sites.Bravo assembly has at least one [SiteGrpcService]-decorated type (BravoGrpcService), " +
                     "so the generated registry should not be empty.");
    }
}
