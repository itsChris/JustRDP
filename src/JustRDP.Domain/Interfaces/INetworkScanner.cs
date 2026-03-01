using System.Net;

namespace JustRDP.Domain.Interfaces;

public class NetworkScanResult
{
    public IPAddress IpAddress { get; set; } = null!;
    public string HostName { get; set; } = string.Empty;
    public List<int> OpenPorts { get; set; } = [];
}

public interface INetworkScanner
{
    Task ScanAsync(
        IEnumerable<IPAddress> hosts,
        List<int> ports,
        int timeoutMs,
        IProgress<int> progress,
        Action<NetworkScanResult> onResult,
        CancellationToken ct);
}
