namespace TestProject.Aggregate.IntegrationTests;

/// <summary>
/// Verifies that MapSiteGrpcServices correctly maps per-site gRPC services
/// using both the AOT-safe registry delegate and the reflection-based assembly scan overloads.
///
/// Covers GRPC-04: MapSiteGrpcServices uses the generated registry for endpoint mapping.
/// </summary>
public sealed class MapSiteGrpcServicesTests
{
    private const string RegistryTypeName = "Muonroi.Tenancy.SiteProfile.Generated.SiteGrpcServiceRegistry";
    private const string GetAllMethodName = "GetAllSiteGrpcServices";

    /// <summary>
    /// Creates a minimal WebApplication configured for gRPC endpoint mapping tests.
    /// Uses a random port to avoid conflicts between tests.
    /// </summary>
    private static WebApplication BuildMinimalGrpcApp()
    {
        string[] args = ["--urls", "http://localhost:0"];
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Services.AddGrpc();
        return builder.Build();
    }

    /// <summary>
    /// GRPC-04: The AOT-safe registry overload MapSiteGrpcServices(Func&lt;IReadOnlyList&lt;TDescriptor&gt;&gt;)
    /// must map BravoGrpcService and return it in the result list.
    ///
    /// This confirms the source-generated registry integrates with the endpoint mapping infrastructure
    /// without requiring assembly reflection at startup.
    /// </summary>
    [Fact]
    public void MapSiteGrpcServices_WithRegistryDelegate_ReturnsBravoMapping()
    {
        // Arrange — get the source-generated registry method via reflection
        Assembly bravoAssembly = typeof(BravoGrpcService).Assembly;
        Type? registryType = bravoAssembly.GetType(RegistryTypeName);
        registryType.Should().NotBeNull(
            because: $"Source generator should emit '{RegistryTypeName}' in the Sites.Bravo assembly.");

        MethodInfo? getAll = registryType!.GetMethod(GetAllMethodName,
            BindingFlags.Public | BindingFlags.Static);
        getAll.Should().NotBeNull(
            because: $"'{RegistryTypeName}' should have a public static '{GetAllMethodName}()' method.");

        // Build a Func<IReadOnlyList<TDescriptor>> delegate from the static method.
        // We use the generic overload MapSiteGrpcServices<TDescriptor> which accepts any class
        // with SiteId, ServiceType, Reason properties — duck-typed inside the extension method.
        Type descriptorListType = getAll!.ReturnType; // IReadOnlyList<GeneratedSiteGrpcServiceDescriptor>
        Type descriptorType = descriptorListType.GenericTypeArguments[0]; // GeneratedSiteGrpcServiceDescriptor

        // Create delegate type: Func<IReadOnlyList<TDescriptor>>
        Type funcType = typeof(Func<>).MakeGenericType(descriptorListType);
        Delegate registryDelegate = getAll.CreateDelegate(funcType);

        // Get generic MapSiteGrpcServices<TDescriptor> method from SiteGrpcEndpointExtensions
        MethodInfo? mapMethod = typeof(SiteGrpcEndpointExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
                m.Name == nameof(SiteGrpcEndpointExtensions.MapSiteGrpcServices) &&
                m.IsGenericMethod);

        mapMethod.Should().NotBeNull(
            because: "SiteGrpcEndpointExtensions should have a generic MapSiteGrpcServices<TDescriptor> overload.");

        MethodInfo genericMap = mapMethod!.MakeGenericMethod(descriptorType);

        // Act — create a fresh app and invoke MapSiteGrpcServices<GeneratedSiteGrpcServiceDescriptor>(delegate)
        WebApplication app = BuildMinimalGrpcApp();
        object? resultObj = genericMap.Invoke(null, [app, registryDelegate]);

        // Assert — result should be IReadOnlyList<(string SiteId, Type ServiceType)>
        resultObj.Should().NotBeNull();
        var mapped = (IReadOnlyList<(string SiteId, Type ServiceType)>)resultObj!;

        mapped.Should().ContainSingle(
            entry => entry.SiteId == "BRAVO" && entry.ServiceType == typeof(BravoGrpcService),
            because: "The AOT-safe registry delegate should produce BravoGrpcService mapped to BRAVO.");
    }

    /// <summary>
    /// GRPC-04 (baseline): The reflection-based assembly scan overload MapSiteGrpcServices(params Assembly[])
    /// must also discover and map BravoGrpcService when the Bravo assembly is provided.
    ///
    /// This confirms the [SiteGrpcService] attribute is correctly placed and the scan works.
    /// </summary>
    [Fact]
    public void MapSiteGrpcServices_WithAssemblyScan_ReturnsBravoMapping()
    {
        // Arrange
        WebApplication app = BuildMinimalGrpcApp();

        // Act — use the reflection-based overload (assembly scan)
        IReadOnlyList<(string SiteId, Type ServiceType)> mapped =
            app.MapSiteGrpcServices(typeof(BravoGrpcService).Assembly);

        // Assert
        mapped.Should().ContainSingle(
            entry => entry.SiteId == "BRAVO" && entry.ServiceType == typeof(BravoGrpcService),
            because: "The assembly scan overload should find BravoGrpcService decorated with [SiteGrpcService(\"BRAVO\")] " +
                     "in the Sites.Bravo assembly.");
    }
}
