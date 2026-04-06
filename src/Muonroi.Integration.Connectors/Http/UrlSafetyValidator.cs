using System.Net;
using System.Net.Sockets;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Integration.Connectors.Http;

/// <summary>
/// Validates URLs against SSRF (Server-Side Request Forgery) attack vectors.
/// Blocks requests to loopback, private IP ranges, link-local, cloud metadata endpoints,
/// and performs DNS resolution to prevent DNS rebinding attacks.
/// </summary>
public static class UrlSafetyValidator
{
    private const string BlockedUrlErrorCode = "CONNECTOR:BLOCKED_URL";
    private const string BlockedUrlMessage = "URL targets a blocked network address";

    /// <summary>
    /// Validates that the given URL is safe to use for an outbound HTTP request.
    /// Throws <see cref="MInternalException"/> with code <c>CONNECTOR:BLOCKED_URL</c>
    /// if the URL targets any blocked address (loopback, private, link-local, metadata).
    /// </summary>
    /// <param name="url">The URL to validate. Must be an absolute http or https URL.</param>
    /// <exception cref="MInternalException">
    /// Thrown when the URL is null, invalid, uses a non-http scheme, or resolves to a blocked address.
    /// </exception>
    public static async Task ValidateAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new MInternalException("URL must not be null or empty", BlockedUrlErrorCode);

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            throw new MInternalException("URL is not a valid absolute URI", BlockedUrlErrorCode);

        // Only http and https schemes are allowed
        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            throw new MInternalException($"URL scheme '{uri.Scheme}' is not allowed; only http and https are permitted", BlockedUrlErrorCode);
        }

        // Check if host is a literal IP address
        if (IPAddress.TryParse(uri.Host, out IPAddress? literalIp))
        {
            if (IsBlockedAddress(literalIp))
                throw new MInternalException(BlockedUrlMessage, BlockedUrlErrorCode);
            return;
        }

        // DNS resolution — checks ALL resolved addresses to prevent DNS rebinding attacks
        // If DNS resolution fails, the host is unreachable; let the HTTP client surface that error
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host);
        }
        catch (SocketException)
        {
            // Cannot determine if the host is safe — allow and let the HTTP client fail naturally
            return;
        }

        foreach (IPAddress ip in addresses)
        {
            if (IsBlockedAddress(ip))
                throw new MInternalException(BlockedUrlMessage, BlockedUrlErrorCode);
        }
    }

    /// <summary>
    /// Returns true if the IP address belongs to any blocked network range.
    /// Blocked ranges:
    /// - 127.0.0.0/8 and ::1 (loopback)
    /// - 10.0.0.0/8 (private Class A)
    /// - 172.16.0.0/12 (private Class B)
    /// - 192.168.0.0/16 (private Class C)
    /// - 169.254.0.0/16 (link-local, AWS IMDSv1 metadata at 169.254.169.254)
    /// - fe80::/10 (IPv6 link-local)
    /// </summary>
    private static bool IsBlockedAddress(IPAddress ip)
    {
        // Normalize IPv4-mapped IPv6 addresses (e.g., ::ffff:10.0.0.1)
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.IsIPv6LinkLocal)
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = ip.GetAddressBytes();
            byte b0 = bytes[0];
            byte b1 = bytes[1];

            // 10.0.0.0/8
            if (b0 == 10)
                return true;

            // 172.16.0.0/12 (172.16.x.x – 172.31.x.x)
            if (b0 == 172 && b1 >= 16 && b1 <= 31)
                return true;

            // 192.168.0.0/16
            if (b0 == 192 && b1 == 168)
                return true;

            // 169.254.0.0/16 (link-local, AWS metadata)
            if (b0 == 169 && b1 == 254)
                return true;
        }

        return false;
    }
}
