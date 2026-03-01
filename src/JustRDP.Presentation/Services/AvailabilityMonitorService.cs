using System.Collections.ObjectModel;
using System.Windows.Threading;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;
using JustRDP.Domain.Interfaces;
using JustRDP.Presentation.ViewModels;
using Microsoft.Extensions.Logging;

namespace JustRDP.Presentation.Services;

public class AvailabilityMonitorService
{
    private readonly IAvailabilityChecker _checker;
    private readonly ISettingsRepository _settings;
    private readonly ILogger<AvailabilityMonitorService> _logger;

    private DispatcherTimer? _timer;
    private TreeViewModel? _treeVm;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private bool _isFirstTick = true;

    public bool IsEnabled { get; private set; }
    public int AvailableCount { get; private set; }
    public int TotalChecked { get; private set; }

    public event Action? SummaryChanged;

    public AvailabilityMonitorService(
        IAvailabilityChecker checker,
        ISettingsRepository settings,
        ILogger<AvailabilityMonitorService> logger)
    {
        _checker = checker;
        _settings = settings;
        _logger = logger;
    }

    public async Task<bool> LoadEnabledStateAsync()
    {
        var value = await _settings.GetAsync("Monitor.Enabled");
        IsEnabled = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
        return IsEnabled;
    }

    public async Task SetEnabledAsync(bool enabled, TreeViewModel treeVm)
    {
        IsEnabled = enabled;
        await _settings.SetAsync("Monitor.Enabled", enabled.ToString());

        if (enabled)
        {
            _treeVm = treeVm;
            StartTimer();
        }
        else
        {
            Stop();
        }
    }

    public void Initialize(TreeViewModel treeVm)
    {
        _treeVm = treeVm;
        if (IsEnabled)
            StartTimer();
    }

    public void Pause()
    {
        _timer?.Stop();
        _cts?.Cancel();
    }

    public void Resume()
    {
        if (IsEnabled)
            _timer?.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
        _cts?.Cancel();
        _cts = null;
        _isFirstTick = true;

        if (_treeVm is not null)
            ResetAllStatuses(_treeVm.RootEntries);

        AvailableCount = 0;
        TotalChecked = 0;
        SummaryChanged?.Invoke();
    }

    private void StartTimer()
    {
        _timer?.Stop();
        _isFirstTick = true;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timer.Tick += async (_, _) => await OnTimerTickAsync();
        _timer.Start();
    }

    private async Task OnTimerTickAsync()
    {
        if (_isFirstTick)
        {
            _isFirstTick = false;
            _timer!.Interval = TimeSpan.FromSeconds(60);
        }

        await RunCheckCycleAsync();
    }

    private async Task RunCheckCycleAsync()
    {
        if (_isRunning || _treeVm is null) return;

        _isRunning = true;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            var pairs = new List<(TreeEntryViewModel Vm, ConnectionEntry Conn)>();
            CollectConnections(_treeVm.RootEntries, pairs);

            if (pairs.Count == 0)
            {
                AvailableCount = 0;
                TotalChecked = 0;
                SummaryChanged?.Invoke();
                return;
            }

            _logger.LogDebug("Availability check starting for {Count} connections", pairs.Count);

            using var semaphore = new SemaphoreSlim(10);
            var available = 0;

            var tasks = pairs.Select(async pair =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var isAvailable = await _checker.IsAvailableAsync(pair.Conn.HostName, pair.Conn.Port, ct);
                    var status = isAvailable ? AvailabilityStatus.Available : AvailabilityStatus.Unavailable;

                    if (isAvailable)
                        Interlocked.Increment(ref available);

                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        pair.Vm.AvailabilityStatus = status;
                    });
                }
                catch (OperationCanceledException)
                {
                    // Check was cancelled — leave status as-is
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Availability check failed for {Host}", pair.Conn.HostName);
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        pair.Vm.AvailabilityStatus = AvailabilityStatus.Unavailable;
                    });
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            if (!ct.IsCancellationRequested)
            {
                AvailableCount = available;
                TotalChecked = pairs.Count;
                SummaryChanged?.Invoke();
                _logger.LogDebug("Availability check complete: {Available}/{Total} available", available, pairs.Count);
            }
        }
        catch (OperationCanceledException)
        {
            // Cycle cancelled
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Availability check cycle failed");
        }
        finally
        {
            _isRunning = false;
        }
    }

    private static void CollectConnections(
        ObservableCollection<TreeEntryViewModel> entries,
        List<(TreeEntryViewModel Vm, ConnectionEntry Conn)> results)
    {
        foreach (var entry in entries)
        {
            if (entry.Entity is ConnectionEntry conn && !string.IsNullOrWhiteSpace(conn.HostName))
                results.Add((entry, conn));

            CollectConnections(entry.Children, results);
        }
    }

    private static void ResetAllStatuses(ObservableCollection<TreeEntryViewModel> entries)
    {
        foreach (var entry in entries)
        {
            entry.AvailabilityStatus = AvailabilityStatus.Unknown;
            ResetAllStatuses(entry.Children);
        }
    }
}
