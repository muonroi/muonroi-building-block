using System.Net;
using System.Net.Sockets;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Validates AI service endpoints to prevent SSRF attacks.
/// Rejects private IPs, link-local, and metadata endpoints.
/// Ollama endpoints are allowed to be local by design.
/// </summary>
internal static class EndpointValidator
{
    /// <summary>
    /// Validates an external endpoint (Claude, OpenAI).
    /// Rejects loopback, private, link-local, and metadata IPs. Requires HTTPS.
    /// </summary>
    internal static string ValidateExternal(string baseEndpoint, string path)
    {
        string raw = $"{baseEndpoint.TrimEnd('/')}{path}";

        if (!Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri))
            throw new ArgumentException($"Invalid endpoint URI: {raw}");

        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"External AI endpoints must use HTTPS. Got: {uri.Scheme}");

        if (IsBlockedHost(uri.Host))
            throw new ArgumentException($"Endpoint host is not allowed: {uri.Host}");

        return uri.AbsoluteUri;
    }

    /// <summary>
    /// Validates a local-allowed endpoint (Ollama).
    /// Allows loopback/private but still blocks metadata and link-local addresses.
    /// </summary>
    internal static string ValidateLocal(string baseEndpoint, string path)
    {
        string raw = $"{baseEndpoint.TrimEnd('/')}{path}";

        if (!Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri))
            throw new ArgumentException($"Invalid endpoint URI: {raw}");

        if (IsMetadataEndpoint(uri.Host))
            throw new ArgumentException($"Cloud metadata endpoints are not allowed: {uri.Host}");

        return uri.AbsoluteUri;
    }

    private static bool IsBlockedHost(string host)
    {
        if (IsMetadataEndpoint(host))
            return true;

        if (IPAddress.TryParse(host, out IPAddress? ip))
            return IsPrivateOrLoopback(ip);

        // Block "localhost" explicitly
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool IsMetadataEndpoint(string host)
    {
        // AWS/GCP/Azure metadata endpoints
        return string.Equals(host, "169.254.169.254", StringComparison.Ordinal)
            || string.Equals(host, "metadata.google.internal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "metadata.azure.internal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivateOrLoopback(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        byte[] bytes = ip.GetAddressBytes();

        return ip.AddressFamily switch
        {
            // IPv4 private ranges: 10.x, 172.16-31.x, 192.168.x
            AddressFamily.InterNetwork =>
                bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254), // link-local
            // IPv6 loopback (::1) and link-local (fe80::)
            AddressFamily.InterNetworkV6 =>
                ip.Equals(IPAddress.IPv6Loopback)
                || ip.IsIPv6LinkLocal,
            _ => false
        };
    }
}
