using System.Net;
using System.Net.Sockets;
using JustRDP.Domain.Interfaces;

namespace JustRDP.Infrastructure.Services;

public class NetworkScanner : INetworkScanner
{
    public async Task ScanAsync(
        IEnumerable<IPAddress> hosts,
        List<int> ports,
        int timeoutMs,
        IProgress<int> progress,
        Action<NetworkScanResult> onResult,
        CancellationToken ct)
    {
        var semaphore = new SemaphoreSlim(16);
        var hostList = hosts.ToList();

        var scanned = 0;
        var tasks = hostList.Select(async ip =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var openPorts = new List<int>();
                foreach (var port in ports)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        using var tcp = new TcpClient();
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        timeoutCts.CancelAfter(timeoutMs);
                        await tcp.ConnectAsync(ip, port, timeoutCts.Token);
                        openPorts.Add(port);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // Port closed or unreachable — expected
                    }
                }

                if (openPorts.Count > 0)
                {
                    var hostName = ip.ToString();
                    try
                    {
                        var entry = await Dns.GetHostEntryAsync(ip);
                        if (!string.IsNullOrWhiteSpace(entry.HostName))
                            hostName = entry.HostName;
                    }
                    catch
                    {
                        // DNS failure is expected — use IP as fallback
                    }

                    onResult(new NetworkScanResult
                    {
                        IpAddress = ip,
                        HostName = hostName,
                        OpenPorts = openPorts
                    });
                }
            }
            finally
            {
                semaphore.Release();
                var done = Interlocked.Increment(ref scanned);
                progress.Report(done);
            }
        });

        await Task.WhenAll(tasks);
    }
}
