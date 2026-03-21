using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;

namespace JustRDP.Presentation.ViewModels;

public class DashboardConnectionItem
{
    public required ConnectionEntry Connection { get; init; }
    public required string Name { get; init; }
    public required string Host { get; init; }
    public required string Protocol { get; init; }
    public required int Port { get; init; }
    public ConnectionType ConnectionType { get; init; }
    public AvailabilityStatus Status { get; init; }
    public DateTime? LastConnectedAt { get; init; }
}

public record DashboardData
{
    public int TotalConnections { get; init; }
    public int OnlineCount { get; init; }
    public int OfflineCount { get; init; }
    public int OpenSessions { get; init; }
    public int RdpCount { get; init; }
    public int SshCount { get; init; }
    public bool IsMonitoringEnabled { get; init; }
    public IReadOnlyList<DashboardConnectionItem> AllConnections { get; init; } = [];
    public IReadOnlyList<DashboardConnectionItem> RecentConnections { get; init; } = [];
}

public partial class DashboardViewModel : ObservableObject
{
    private readonly Func<ConnectionEntry, Task>? _connectCallback;
    private readonly Func<Guid, bool>? _connectionExistsCallback;
    private readonly Action? _refreshCallback;

    [ObservableProperty]
    private int _totalConnections;

    [ObservableProperty]
    private int _onlineCount;

    [ObservableProperty]
    private int _offlineCount;

    [ObservableProperty]
    private int _openSessions;

    [ObservableProperty]
    private int _rdpCount;

    [ObservableProperty]
    private int _sshCount;

    [ObservableProperty]
    private bool _isMonitoringEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isLoading = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasConnections;

    [ObservableProperty]
    private bool _hasRecentConnections;

    [ObservableProperty]
    private string _connectionFilterText = string.Empty;

    public bool ShowEmptyState => !HasConnections && !IsLoading;

    private readonly List<DashboardConnectionItem> _allConnectionsSource = [];
    public ObservableCollection<DashboardConnectionItem> AllConnections { get; } = [];
    public ObservableCollection<DashboardConnectionItem> RecentConnections { get; } = [];

    partial void OnConnectionFilterTextChanged(string value)
    {
        ApplyConnectionFilter();
    }

    private void ApplyConnectionFilter()
    {
        AllConnections.Clear();
        var filter = ConnectionFilterText.Trim();
        foreach (var item in _allConnectionsSource)
        {
            if (string.IsNullOrEmpty(filter) ||
                item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Host.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Protocol.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                AllConnections.Add(item);
            }
        }
    }

    public DashboardViewModel(
        Func<ConnectionEntry, Task>? connectCallback = null,
        Func<Guid, bool>? connectionExistsCallback = null,
        Action? refreshCallback = null)
    {
        _connectCallback = connectCallback;
        _connectionExistsCallback = connectionExistsCallback;
        _refreshCallback = refreshCallback;
    }

    [RelayCommand]
    private async Task ConnectFromDashboard(DashboardConnectionItem? item)
    {
        if (item is null || _connectCallback is null) return;

        // Validate connection still exists in tree (may have been deleted since last refresh)
        if (_connectionExistsCallback is not null && !_connectionExistsCallback(item.Connection.Id))
        {
            _refreshCallback?.Invoke();
            return;
        }

        await _connectCallback(item.Connection);
    }

    public void Refresh(DashboardData data)
    {
        IsLoading = false;
        TotalConnections = data.TotalConnections;
        OnlineCount = data.OnlineCount;
        OfflineCount = data.OfflineCount;
        OpenSessions = data.OpenSessions;
        RdpCount = data.RdpCount;
        SshCount = data.SshCount;
        IsMonitoringEnabled = data.IsMonitoringEnabled;
        HasConnections = data.TotalConnections > 0;

        _allConnectionsSource.Clear();
        _allConnectionsSource.AddRange(data.AllConnections);
        ApplyConnectionFilter();

        RecentConnections.Clear();
        foreach (var item in data.RecentConnections)
            RecentConnections.Add(item);

        HasRecentConnections = RecentConnections.Count > 0;
    }
}
