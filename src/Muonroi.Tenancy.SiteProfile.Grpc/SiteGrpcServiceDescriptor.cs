namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// Descriptor for a site-specific gRPC service discovered at compile-time via [SiteGrpcService].
/// Produced by the source generator into SiteGrpcServiceRegistry.g.cs and consumed by
/// MapSiteGrpcServices(Func&lt;IReadOnlyList&lt;SiteGrpcServiceDescriptor&gt;&gt;) at startup.
/// AOT-safe — no reflection.
/// </summary>
/// <param name="SiteId">The site identifier this gRPC service belongs to.</param>
/// <param name="ServiceType">The concrete gRPC service implementation type.</param>
/// <param name="Reason">Optional human-readable note about why this service exists.</param>
public sealed record SiteGrpcServiceDescriptor(
    string SiteId,
    Type ServiceType,
    string? Reason = null);
