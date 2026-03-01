namespace JustRDP.Domain.Interfaces;

public interface IAvailabilityChecker
{
    Task<bool> IsAvailableAsync(string hostName, int port, CancellationToken ct);
}
