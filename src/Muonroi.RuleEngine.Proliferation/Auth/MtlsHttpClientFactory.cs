using System.Collections.Concurrent;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Auth;

/// <summary>
/// Creates and caches HttpClient instances pre-configured with mTLS client certificates.
/// Supports PFX (.pfx, .p12) and PEM (.pem, .crt) certificate formats.
/// Clients are cached per projectId; the same instance is reused across requests.
/// </summary>
/// <remarks>Creates a factory that builds mTLS HttpClient instances.</remarks>
public sealed class MtlsHttpClientFactory(IMLog<MtlsHttpClientFactory>? logger = null) : IMtlsHttpClientFactory
{
    private readonly IMLog<MtlsHttpClientFactory>? _logger = logger;

    // Cache: projectId -> HttpClient with configured mTLS handler
    private readonly ConcurrentDictionary<string, HttpClient> _clientCache = new();

    /// <inheritdoc/>
    public HttpClient CreateClient(string projectId, MtlsConfig config)
    {
        return _clientCache.GetOrAdd(projectId, _ => BuildClient(config));
    }

    private HttpClient BuildClient(MtlsConfig config)
    {
        X509Certificate2 clientCert = LoadCertificate(config);

        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(clientCert);

        // If custom CA certificate provided, configure custom server validation
        if (!string.IsNullOrWhiteSpace(config.CaCertificatePath))
        {
            X509Certificate2 caCert = new(config.CaCertificatePath);
            handler.ServerCertificateCustomValidationCallback = (_, cert, chain, _) =>
            {
                if (cert == null || chain == null)
                    return false;

                chain.ChainPolicy.ExtraStore.Add(caCert);
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                return chain.Build(cert);
            };
        }

        _logger?.Info("mTLS HttpClient created with certificate from {CertPath}", config.CertificatePath);
        return new HttpClient(handler);
    }

    private static X509Certificate2 LoadCertificate(MtlsConfig config)
    {
        string extension = Path.GetExtension(config.CertificatePath).ToLowerInvariant();

        if (extension is ".pfx" or ".p12")
        {
            return string.IsNullOrEmpty(config.CertificatePassword)
                ? new X509Certificate2(config.CertificatePath)
                : new X509Certificate2(config.CertificatePath, config.CertificatePassword);
        }

        // PEM / CRT — load as PEM (requires .NET 5+)
        return X509Certificate2.CreateFromPemFile(config.CertificatePath);
    }
}
