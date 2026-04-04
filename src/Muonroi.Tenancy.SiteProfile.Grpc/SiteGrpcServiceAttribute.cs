using Muonroi.Core.Abstractions.Guards;
namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// Marks a gRPC service implementation as site-specific.
/// Used by <see cref="SiteGrpcEndpointExtensions.MapSiteGrpcServices"/> to auto-discover
/// and register per-site gRPC endpoints.
///
/// <para>
/// <b>Two proto modes:</b>
/// <list type="bullet">
///   <item><b>Shared proto (default):</b> All sites share one .proto file.
///   Site differences handled via optional fields + C# service inheritance.
///   Register with <c>MapGrpcService&lt;T&gt;()</c> as usual.</item>
///   <item><b>Per-site proto:</b> Site has completely different .proto structure.
///   Mark the gRPC service implementation with this attribute.
///   Register via <c>MapSiteGrpcServices()</c> for auto-discovery.</item>
/// </list>
/// </para>
///
/// <example>
/// <code>
/// // Per-site proto: TCI has its own service definition
/// [SiteGrpcService("TCI")]
/// public class TciFcdGrpcService : TciFullContainerDelivery.TciFullContainerDeliveryBase
/// {
///     // Implements TCI-specific RPCs from tci.fcd.proto
/// }
///
/// // In Program.cs:
/// app.MapSiteGrpcServices(typeof(TciFcdGrpcService).Assembly);
/// </code>
/// </example>
/// </summary>
/// <remarks>
/// Creates a new site gRPC service marker.
/// </remarks>
/// <param name="siteId">Site identifier matching <see cref="ISiteProfile.SiteId"/>.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class SiteGrpcServiceAttribute(string siteId) : Attribute
{
    /// <summary>Site identifier (e.g., "TCI", "CTL").</summary>
    public string SiteId { get; } = MGuard.NotNull(siteId);

    /// <summary>
    /// Optional description of why this site needs a separate proto.
    /// For documentation/logging only.
    /// </summary>
    public string? Reason { get; set; }
}
