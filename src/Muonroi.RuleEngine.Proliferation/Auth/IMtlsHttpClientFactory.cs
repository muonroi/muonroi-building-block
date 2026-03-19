using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Auth;

/// <summary>
/// Creates HttpClient instances pre-configured with mTLS client certificates.
/// Implementations should cache clients per projectId to avoid repeated certificate loading.
/// </summary>
public interface IMtlsHttpClientFactory
{
    /// <summary>
    /// Returns an HttpClient configured with the X.509 client certificate from the given config.
    /// The client is cached per projectId — the same instance is returned on subsequent calls
    /// with the same projectId.
    /// </summary>
    HttpClient CreateClient(string projectId, MtlsConfig config);
}
