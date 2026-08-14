namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// Marks a gRPC service implementation as site-specific.
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
/// <param name="siteId">Site identifier matching <see cref="ISiteProfile.SiteId"/>.</param>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class SiteGrpcServiceAttribute(string siteId) : Attribute
{
    /// <summary>
    /// Gets the site identifier that this gRPC service is associated with.
    /// </summary>
    public string SiteId { get; } = MGuard.NotNull(siteId);

    /// <summary>
    /// Optional description of why this site needs a separate proto.
    /// For documentation/logging only.
    /// </summary>
    public string? Reason { get; set; }
}
