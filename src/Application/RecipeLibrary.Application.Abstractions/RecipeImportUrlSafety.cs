using System.Net;
using System.Net.Sockets;

namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Blocks SSRF-prone URL import targets (loopback, private, link-local, metadata hosts).
/// </summary>
public static class RecipeImportUrlSafety
{
    private static readonly HashSet<string> BlockedHostNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "metadata.google.internal",
        "metadata",
    };

    public static async Task EnsurePublicHttpUrlAsync(string url, CancellationToken ct = default)
    {
        _ = await ResolvePublicHttpEndpointAsync(url, ct);
    }

    public static async Task EnsurePublicHttpUrlAsync(Uri uri, CancellationToken ct = default)
    {
        _ = await ResolvePublicHttpEndpointAsync(uri, ct);
    }

    /// <summary>
    /// Validates the URL and returns the DNS addresses that passed the public-host check.
    /// Callers should connect only to these addresses to prevent DNS rebinding.
    /// </summary>
    public static async Task<PublicHttpEndpoint> ResolvePublicHttpEndpointAsync(
        string url,
        CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("URL must be an absolute http or https address.");
        }

        return await ResolvePublicHttpEndpointAsync(uri, ct);
    }

    public static async Task<PublicHttpEndpoint> ResolvePublicHttpEndpointAsync(
        Uri uri,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.Scheme is not "http" and not "https")
        {
            throw new ArgumentException("URL must be an absolute http or https address.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("URL host is required.");
        }

        if (uri.IsLoopback
            || BlockedHostNames.Contains(uri.Host)
            || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("URL host is not allowed.");
        }

        if (IPAddress.TryParse(uri.Host, out var literalIp))
        {
            if (IsBlockedAddress(literalIp))
            {
                throw new ArgumentException("URL host is not allowed.");
            }

            return new PublicHttpEndpoint(uri, [literalIp]);
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host, ct);
        }
        catch (SocketException)
        {
            throw new ArgumentException("URL host could not be resolved.");
        }

        if (addresses.Length == 0 || addresses.Any(IsBlockedAddress))
        {
            throw new ArgumentException("URL host is not allowed.");
        }

        return new PublicHttpEndpoint(uri, addresses);
    }

    public static bool IsBlockedAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            // 0.0.0.0/8
            if (bytes[0] == 0)
            {
                return true;
            }

            // 10.0.0.0/8
            if (bytes[0] == 10)
            {
                return true;
            }

            // 100.64.0.0/10 (CGNAT)
            if (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
            {
                return true;
            }

            // 127.0.0.0/8
            if (bytes[0] == 127)
            {
                return true;
            }

            // 169.254.0.0/16 (link-local / cloud metadata)
            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return true;
            }

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            {
                return true;
            }

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }

            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6Loopback))
            {
                return true;
            }

            var bytes = address.GetAddressBytes();
            // fe80::/10 link-local
            if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)
            {
                return true;
            }

            // fc00::/7 unique local
            if ((bytes[0] & 0xfe) == 0xfc)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// A validated http(s) URL plus the DNS addresses that were confirmed public at resolve time.
/// </summary>
public readonly record struct PublicHttpEndpoint(Uri Uri, IReadOnlyList<IPAddress> Addresses);
