namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// Scoped holder for the current request's SiteCode.
/// Set by <see cref="SiteCodeGrpcInterceptor"/>, read by ISiteProfileResolver's siteCodeAccessor.
///
/// Usage in consumer's siteCodeAccessor:
/// <code>
/// siteCodeAccessor: sp => sp.GetService&lt;ISiteCodeHolder&gt;()?.SiteCode
///     ?? sp.GetService&lt;IHttpContextAccessor&gt;()?.HttpContext?.Items["__site_code"]?.ToString()
/// </code>
/// </summary>
public interface ISiteCodeHolder
{
    /// <summary>Current request's SiteCode. Null if not yet resolved.</summary>
    string? SiteCode { get; set; }
}
