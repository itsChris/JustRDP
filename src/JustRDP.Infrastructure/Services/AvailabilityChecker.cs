using System.Net.NetworkInformation;
using System.Net.Sockets;
using JustRDP.Domain.Interfaces;

namespace JustRDP.Infrastructure.Services;

public class AvailabilityChecker : IAvailabilityChecker
{
    public async Task<bool> IsAvailableAsync(string hostName, int port, CancellationToken ct)
    {
        // Step 1: ICMP ping (fast path)
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(hostName, 2000);
            if (reply.Status == IPStatus.Success)
                return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Ping may be blocked by firewall — fall through to TCP
        }

        // Step 2: TCP port probe (fallback)
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(3000);

            using var client = new TcpClient();
            await client.ConnectAsync(hostName, port, cts.Token);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
