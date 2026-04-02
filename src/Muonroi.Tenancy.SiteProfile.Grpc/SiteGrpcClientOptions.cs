namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// Describes a per-site gRPC client registration.
/// Stored in <see cref="SiteGrpcClientRegistry"/> at startup; consumed by <see cref="ISiteGrpcClientFactory"/> at runtime.
/// </summary>
/// <param name="SiteId">The site key (e.g., "TCI", "DEFAULT"). Matched against <c>ISiteProfile.SiteId</c>.</param>
/// <param name="ServiceName">The named service key supplied to <c>AddSiteGrpcClient</c>.</param>
/// <param name="ClientType">The concrete gRPC client type to instantiate.</param>
public sealed record SiteGrpcClientDescriptor(string SiteId, string ServiceName, Type ClientType);

/// <summary>
/// Singleton registry holding all <see cref="SiteGrpcClientDescriptor"/> entries added via
/// <c>SiteGrpcExtensions.AddSiteGrpcClient&lt;TClient&gt;</c>.
/// </summary>
public sealed class SiteGrpcClientRegistry
{
    private readonly List<SiteGrpcClientDescriptor> _descriptors = [];

    /// <summary>All registered descriptors. Read-only after startup.</summary>
    public IReadOnlyList<SiteGrpcClientDescriptor> Descriptors => _descriptors;

    /// <summary>Adds a descriptor. Called only during DI registration (startup).</summary>
    internal void Add(SiteGrpcClientDescriptor descriptor) => _descriptors.Add(descriptor);
}
