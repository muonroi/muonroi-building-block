namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// Applied to a partial interface to generate a unified gRPC facade client
/// that combines shared + per-site proto RPCs into one interface.
///
/// <para>
/// The source generator scans the <see cref="SharedClient"/> and all <see cref="ExtendClients"/>
/// for public Async RPC methods and emits:
/// <list type="number">
///   <item>A completion of the partial interface with all extracted RPC method signatures.</item>
///   <item>A concrete <c>{InterfaceName}Facade</c> implementation class that delegates each call
///       to the correct inner client (shared or per-site extend).</item>
/// </list>
/// </para>
///
/// <example>
/// <code>
/// // Aggregate project — define the facade contract:
/// [GenerateSiteGrpcFacade(
///     SharedClient = typeof(FullContainerDeliveryClient),
///     ExtendClients = new[] { typeof(TciFullContainerDeliveryClient) })]
/// public partial interface ITciFcdClient { }
///
/// // Register in Program.cs:
/// services.AddSiteGrpcFacadeClient&lt;ITciFcdClient, TciFcdClientFacade&gt;("TCI", "fcd");
/// services.AddSiteGrpcFacadeClient&lt;ITciFcdClient, TciFcdClientFacade&gt;("default", "fcd");
///
/// // Resolve in aggregate service:
/// var facade = factory.CreateFacadeForCurrentSite&lt;ITciFcdClient&gt;("fcd");
/// var result = await facade.CreateV4Async(request);
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class GenerateSiteGrpcFacadeAttribute : Attribute
{
    /// <summary>
    /// The shared gRPC client type (e.g., <c>typeof(FullContainerDeliveryClient)</c>).
    /// All Async RPC methods on this type are included in the facade.
    /// </summary>
    public Type SharedClient { get; set; } = null!;

    /// <summary>
    /// Per-site gRPC client types to merge into the facade.
    /// When an extend client has a method with the same name as the shared client,
    /// the extend version wins (per-site override semantics).
    /// Empty array = shared-only facade.
    /// </summary>
    public Type[] ExtendClients { get; set; } = [];
}
