using System.Net;
using System.Net.Sockets;

namespace EnrichedMessaging.Infrastructure.Services;

/// <summary>
/// Blocks outbound HTTP requests to private, loopback, and link-local IP ranges
/// to prevent Server-Side Request Forgery (SSRF) attacks.
/// </summary>
internal sealed class SsrfGuardHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var host = request.RequestUri?.Host
            ?? throw new InvalidOperationException("Request URI has no host.");

        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        if (addresses.Length == 0)
            throw new InvalidOperationException($"Could not resolve host: {host}");

        foreach (var address in addresses)
        {
            if (IsPrivateOrReserved(address))
                throw new InvalidOperationException(
                    $"Link preview fetch blocked: '{host}' resolves to a private or reserved address.");
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static bool IsPrivateOrReserved(IPAddress address)
    {
        // Normalise IPv4-mapped IPv6 (::ffff:x.x.x.x) to plain IPv4
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return
                bytes[0] == 127 ||                                          // 127.0.0.0/8  loopback
                bytes[0] == 10 ||                                           // 10.0.0.0/8   RFC 1918
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||   // 172.16.0.0/12 RFC 1918
                (bytes[0] == 192 && bytes[1] == 168) ||                     // 192.168.0.0/16 RFC 1918
                (bytes[0] == 169 && bytes[1] == 254) ||                     // 169.254.0.0/16 link-local / metadata
                bytes[0] == 0;                                              // 0.0.0.0/8    "this" network
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(address)) return true;                 // ::1
            var bytes = address.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC) return true;                    // fc00::/7 ULA
            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80) return true; // fe80::/10 link-local
        }

        return false;
    }
}
