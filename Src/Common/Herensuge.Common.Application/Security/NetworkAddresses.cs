using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Herensuge.Common.Application.Security;

/// <summary>Trusted networks of an organization (plan D9): public CIDR ranges, IPv4 or IPv6.</summary>
public static class NetworkAddresses
{
    public const int MaxTrustedNetworks = 50;

    // Never a client's public address: private, loopback, link-local, CGNAT, unspecified, unique local, multicast.
    private static readonly IPNetwork[] NonPublic =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("224.0.0.0/4"),
        IPNetwork.Parse("::/127"),
        IPNetwork.Parse("fc00::/7"),
        IPNetwork.Parse("fe80::/10"),
        IPNetwork.Parse("ff00::/8"),
    ];

    /// <summary>"203.0.113.0/24", "2001:db8::/32" or a single address (/32, /128), with no host bits set.</summary>
    public static bool TryParse(string? value, [NotNullWhen(true)] out IPNetwork? network)
    {
        network = null;
        string text = value?.Trim() ?? string.Empty;
        if (!text.Contains('/', StringComparison.Ordinal) && IPAddress.TryParse(text, out IPAddress? single))
        {
            text = $"{text}/{(single.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128)}";
        }

        // "203.0.113.7/24" is ambiguous (a typo for /32?): only the canonical base address is accepted.
        if (!IPNetwork.TryParse(text, out IPNetwork parsed)
            || !IPAddress.TryParse(text[..text.IndexOf('/', StringComparison.Ordinal)], out IPAddress? address)
            || !address.Equals(parsed.BaseAddress))
        {
            return false;
        }

        network = parsed;
        return true;
    }

    /// <summary>A range an organization may trust: valid, public and not overlapping any non-public range.</summary>
    public static bool IsPublicNetwork(string? value) =>
        TryParse(value, out IPNetwork? network)
        && !NonPublic.Any(range => range.BaseAddress.AddressFamily == network.Value.BaseAddress.AddressFamily
            && (range.Contains(network.Value.BaseAddress) || network.Value.Contains(range.BaseAddress)));

    /// <summary>False for private, loopback and link-local addresses: behind a proxy, a sign of a misconfiguration.</summary>
    public static bool IsPublic(IPAddress address)
    {
        IPAddress normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return !NonPublic.Any(range => range.Contains(normalized));
    }

    /// <summary>Plan D9: no list → any public address; otherwise the address must be in one of the ranges.</summary>
    public static bool IsTrusted(IPAddress? address, IReadOnlyCollection<string> trustedNetworks)
    {
        if (address is null || !IsPublic(address))
        {
            return false;
        }

        IPAddress normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return trustedNetworks.Count == 0
            || trustedNetworks.Any(value => TryParse(value, out IPNetwork? network) && network.Value.Contains(normalized));
    }
}
