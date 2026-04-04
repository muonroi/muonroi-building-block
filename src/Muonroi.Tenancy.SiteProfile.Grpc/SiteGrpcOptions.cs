namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// Configuration options for gRPC site resolution.
/// Consumer defines which metadata key(s) carry the SiteCode.
///
/// <example>
/// <code>
/// services.AddSiteGrpcServices(o =>
/// {
///     o.MetadataKey = "SiteCode";            // PascalCase convention
///     o.HttpHeaderFallbackKey = "x-site-code"; // HTTP fallback
///     o.Required = true;                      // throw if missing
/// });
/// </code>
/// </example>
/// </summary>
public sealed class SiteGrpcOptions
{
    /// <summary>
    /// gRPC metadata key to extract SiteCode from.
    /// Default: "SiteCode". Consumer overrides to match their convention.
    /// </summary>
    public string MetadataKey { get; set; } = "SiteCode";

    /// <summary>
    /// HTTP header fallback key (for gRPC-Web, REST gateway, or mixed transports).
    /// Default: same as <see cref="MetadataKey"/>.
    /// Set null to disable HTTP header fallback.
    /// </summary>
    public string? HttpHeaderFallbackKey { get; set; }

    /// <summary>
    /// Whether SiteCode is required on every request.
    /// If true and SiteCode not found: throws RpcException(InvalidArgument).
    /// If false and SiteCode not found: continues without setting ISiteCodeHolder.
    /// Default: true.
    /// </summary>
    public bool Required { get; set; } = true;
}
