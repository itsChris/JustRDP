using Microsoft.Win32;

namespace JustRDP.Infrastructure.Import;

public static class MstscRegistryReader
{
    public static List<MstscMruEntry> ReadMruEntries()
    {
        var entries = new List<MstscMruEntry>();

        using var defaultKey = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Terminal Server Client\Default");
        if (defaultKey is null)
            return entries;

        // MRU values are named MRU0, MRU1, ... MRU9 (sometimes more)
        var valueNames = defaultKey.GetValueNames()
            .Where(n => n.StartsWith("MRU", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n);

        foreach (var valueName in valueNames)
        {
            if (defaultKey.GetValue(valueName) is not string host || string.IsNullOrWhiteSpace(host))
                continue;

            // Parse host:port format
            int port = 3389;
            var hostName = host;
            var lastColon = host.LastIndexOf(':');
            if (lastColon > 0 && int.TryParse(host[(lastColon + 1)..], out var parsedPort))
            {
                hostName = host[..lastColon];
                port = parsedPort;
            }

            // Look up username from per-server key
            string? username = null;
            using var serverKey = Registry.CurrentUser.OpenSubKey(
                $@"Software\Microsoft\Terminal Server Client\Servers\{host}");
            if (serverKey is not null)
            {
                username = serverKey.GetValue("UsernameHint") as string;
            }

            entries.Add(new MstscMruEntry(hostName, port, username));
        }

        return entries;
    }
}

public record MstscMruEntry(string HostName, int Port, string? Username);
