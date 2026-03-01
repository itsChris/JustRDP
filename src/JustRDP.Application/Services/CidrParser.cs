using System.Net;

namespace JustRDP.Application.Services;

public static class CidrParser
{
    public static readonly HashSet<int> SshPorts = [22, 2222];

    public record CidrParseResult(bool IsValid, string? Error, IPAddress? First, IPAddress? Last, int HostCount, bool RequiresConfirmation);

    public static CidrParseResult ParseRange(string cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr))
            return new CidrParseResult(false, "CIDR range is required.", null, null, 0, false);

        var parts = cidr.Trim().Split('/');
        if (parts.Length != 2)
            return new CidrParseResult(false, "Invalid CIDR notation. Expected format: 192.168.1.0/24", null, null, 0, false);

        if (!IPAddress.TryParse(parts[0], out var ip))
            return new CidrParseResult(false, "Invalid IP address.", null, null, 0, false);

        if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 32)
            return new CidrParseResult(false, "Invalid prefix length. Must be 16-32.", null, null, 0, false);

        if (prefix < 16)
            return new CidrParseResult(false, "Range too large. Maximum allowed is /16 (65,534 hosts).", null, null, 0, false);

        var ipBytes = ip.GetAddressBytes();
        var ipUint = (uint)(ipBytes[0] << 24 | ipBytes[1] << 16 | ipBytes[2] << 8 | ipBytes[3]);

        if (prefix == 32)
        {
            return new CidrParseResult(true, null, ip, ip, 1, false);
        }

        var mask = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
        var network = ipUint & mask;
        var broadcast = network | ~mask;

        if (prefix == 31)
        {
            // Point-to-point: both IPs are usable
            var first31 = UintToIp(network);
            var last31 = UintToIp(broadcast);
            return new CidrParseResult(true, null, first31, last31, 2, false);
        }

        // Standard: exclude network and broadcast
        var firstHost = UintToIp(network + 1);
        var lastHost = UintToIp(broadcast - 1);
        var hostCount = (int)(broadcast - network - 1);
        var requiresConfirmation = prefix < 20;

        return new CidrParseResult(true, null, firstHost, lastHost, hostCount, requiresConfirmation);
    }

    public static List<IPAddress> EnumerateHosts(string cidr)
    {
        var result = ParseRange(cidr);
        if (!result.IsValid || result.First is null || result.Last is null)
            return [];

        var first = IpToUint(result.First);
        var last = IpToUint(result.Last);

        var hosts = new List<IPAddress>(result.HostCount);
        for (var i = first; i <= last; i++)
        {
            hosts.Add(UintToIp(i));
        }
        return hosts;
    }

    public static (bool IsValid, List<int> Ports, string? Error) ParsePorts(string? input, int defaultPort)
    {
        var ports = new HashSet<int> { defaultPort };

        if (string.IsNullOrWhiteSpace(input))
            return (true, ports.ToList(), null);

        foreach (var raw in input.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = raw.Trim();
            if (!int.TryParse(trimmed, out var port))
                return (false, [], $"Invalid port: {trimmed}. Ports must be 1-65535.");
            if (port < 1 || port > 65535)
                return (false, [], $"Invalid port: {trimmed}. Ports must be 1-65535.");
            ports.Add(port);
        }

        return (true, ports.ToList(), null);
    }

    private static uint IpToUint(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
    }

    private static IPAddress UintToIp(uint value)
    {
        return new IPAddress(new[]
        {
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value
        });
    }
}
