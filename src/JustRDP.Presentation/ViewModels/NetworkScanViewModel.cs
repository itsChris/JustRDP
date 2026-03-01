using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustRDP.Application.Services;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;
using JustRDP.Domain.Interfaces;
using Serilog;

namespace JustRDP.Presentation.ViewModels;

public partial class NetworkScanViewModel : ObservableObject
{
    private readonly INetworkScanner _scanner;
    private readonly TreeService _treeService;
    private readonly ITreeEntryRepository _repository;
    private CancellationTokenSource? _cts;
    private DispatcherTimer? _batchTimer;
    private readonly ConcurrentQueue<ScanResultRow> _pendingResults = new();
    private HashSet<(string Host, ConnectionType Protocol)> _existingHosts = [];
    private bool _wasCancelled;

    [ObservableProperty]
    private string _cidrInput = string.Empty;

    [ObservableProperty]
    private int _defaultPort = 3389;

    [ObservableProperty]
    private string _additionalPorts = "22";

    [ObservableProperty]
    private int _timeoutMs = 1500;

    [ObservableProperty]
    private string _rangeTranslation = string.Empty;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private string? _portValidationError;

    [ObservableProperty]
    private string? _timeoutValidationError;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private int _progressMax = 1;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private bool _hasScanRun;

    [ObservableProperty]
    private string _resultsHeader = string.Empty;

    [ObservableProperty]
    private FolderOption? _selectedFolder;

    public ObservableCollection<ScanResultRow> Results { get; } = [];
    public ObservableCollection<FolderOption> TargetFolders { get; } = [];

    public int SelectedCount => Results.Count(r => r.IsChecked);

    /// <summary>
    /// Raised after import so the main window can reload the tree immediately.
    /// </summary>
    public event Func<Task>? ImportCompleted;

    public NetworkScanViewModel(
        INetworkScanner scanner,
        TreeService treeService,
        ITreeEntryRepository repository)
    {
        _scanner = scanner;
        _treeService = treeService;
        _repository = repository;
    }

    partial void OnCidrInputChanged(string value) => UpdateRangeTranslation();

    private void UpdateRangeTranslation()
    {
        if (string.IsNullOrWhiteSpace(CidrInput))
        {
            RangeTranslation = string.Empty;
            ValidationError = null;
            return;
        }

        var result = CidrParser.ParseRange(CidrInput);
        if (!result.IsValid)
        {
            RangeTranslation = string.Empty;
            ValidationError = result.Error;
            return;
        }

        ValidationError = null;
        RangeTranslation = result.HostCount == 1
            ? $"{result.First} (1 host)"
            : $"{result.First} - {result.Last} ({result.HostCount:N0} hosts)";
    }

    public async Task LoadFoldersAsync()
    {
        TargetFolders.Clear();
        TargetFolders.Add(new FolderOption(null, "(Root)"));

        var entries = await _treeService.GetAllEntriesAsync();
        foreach (var entry in entries.OfType<FolderEntry>().OrderBy(e => e.Name))
        {
            TargetFolders.Add(new FolderOption(entry.Id, entry.Name));
        }

        SelectedFolder = TargetFolders.FirstOrDefault();
    }

    [RelayCommand]
    private async Task StartScan()
    {
        // Validate CIDR
        var parseResult = CidrParser.ParseRange(CidrInput);
        if (!parseResult.IsValid)
        {
            ValidationError = parseResult.Error;
            return;
        }

        // Validate ports
        var portResult = CidrParser.ParsePorts(AdditionalPorts, DefaultPort);
        if (!portResult.IsValid)
        {
            PortValidationError = portResult.Error;
            return;
        }
        PortValidationError = null;

        // Validate timeout
        if (TimeoutMs < 200 || TimeoutMs > 10000)
        {
            TimeoutValidationError = "Timeout must be between 200 and 10,000 ms.";
            return;
        }
        TimeoutValidationError = null;

        // Confirmation for large ranges
        if (parseResult.RequiresConfirmation)
        {
            var confirm = System.Windows.MessageBox.Show(
                $"This range contains {parseResult.HostCount:N0} hosts. Scanning may take a while. Continue?",
                "Large Range",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            if (confirm != System.Windows.MessageBoxResult.Yes)
                return;
        }

        var hosts = CidrParser.EnumerateHosts(CidrInput);
        var ports = portResult.Ports;

        // Load existing hosts for detection
        await LoadExistingHostsAsync();

        // Reset state
        Results.Clear();
        _pendingResults.Clear();
        _wasCancelled = false;
        HasScanRun = true;
        Progress = 0;
        ProgressMax = hosts.Count;
        ProgressText = $"0 / {hosts.Count} hosts";
        ResultsHeader = "Scanning...";
        IsScanning = true;
        OnPropertyChanged(nameof(SelectedCount));

        _cts = new CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew();

        Log.Information("Network scan started: CIDR={Cidr}, ports=[{Ports}], hosts={HostCount}, timeout={Timeout}ms",
            CidrInput, string.Join(", ", ports), hosts.Count, TimeoutMs);

        // Start batch timer to drain results onto the UI thread
        _batchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _batchTimer.Tick += (_, _) => DrainPendingResults();
        _batchTimer.Start();

        var progressReporter = new Progress<int>(scanned =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                Progress = scanned;
                ProgressText = $"{scanned} / {ProgressMax} hosts";
            });
        });

        try
        {
            await _scanner.ScanAsync(hosts, ports, TimeoutMs, progressReporter, OnHostScanned, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _wasCancelled = true;
            Log.Information("Network scan cancelled by user after {Elapsed}ms", stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            stopwatch.Stop();
            _batchTimer.Stop();
            DrainPendingResults();
            IsScanning = false;

            if (_wasCancelled)
            {
                ResultsHeader = $"Scan cancelled. {Results.Count} result(s) found ({Progress} / {ProgressMax} scanned)";
            }
            else if (Results.Count > 0)
            {
                ResultsHeader = $"{Results.Count} result(s) found";
            }
            else
            {
                ResultsHeader = "No hosts found. The range may be unreachable or ports may be blocked.";
            }

            Log.Information("Network scan completed: {ResultCount} hosts found in {Elapsed}ms",
                Results.Count, stopwatch.ElapsedMilliseconds);
        }
    }

    private void OnHostScanned(NetworkScanResult sr)
    {
        var sshPorts = sr.OpenPorts.Where(p => CidrParser.SshPorts.Contains(p)).ToList();
        var rdpPorts = sr.OpenPorts.Where(p => !CidrParser.SshPorts.Contains(p)).ToList();
        var hasBoth = sshPorts.Count > 0 && rdpPorts.Count > 0;

        if (rdpPorts.Count > 0)
        {
            var name = hasBoth ? $"{sr.HostName} (RDP)" : sr.HostName;
            var exists = CheckHostExists(sr.IpAddress.ToString(), sr.HostName, ConnectionType.RDP);
            _pendingResults.Enqueue(new ScanResultRow(sr.IpAddress, name, rdpPorts, exists, ConnectionType.RDP, this));
        }

        if (sshPorts.Count > 0)
        {
            var name = hasBoth ? $"{sr.HostName} (SSH)" : sr.HostName;
            var exists = CheckHostExists(sr.IpAddress.ToString(), sr.HostName, ConnectionType.SSH);
            _pendingResults.Enqueue(new ScanResultRow(sr.IpAddress, name, sshPorts, exists, ConnectionType.SSH, this));
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void SelectAllNew()
    {
        foreach (var row in Results)
        {
            if (!row.ExistsInDatabase)
                row.IsChecked = true;
        }
        OnPropertyChanged(nameof(SelectedCount));
    }

    [RelayCommand]
    private async Task ImportSelected()
    {
        var selected = Results.Where(r => r.IsChecked && !r.ExistsInDatabase).ToList();
        if (selected.Count == 0) return;

        var parentId = SelectedFolder?.Id;
        var folderName = SelectedFolder?.Name ?? "(Root)";

        foreach (var row in selected)
        {
            var name = row.HostName != row.IpAddress.ToString() ? row.HostName : row.IpAddress.ToString();
            var port = row.SelectedPort;
            await _treeService.CreateConnectionAsync(name, row.IpAddress.ToString(), parentId, port, row.Protocol);
            row.ExistsInDatabase = true;
            row.IsChecked = false;
            _existingHosts.Add((row.IpAddress.ToString().ToLowerInvariant(), row.Protocol));
            if (row.HostName != row.IpAddress.ToString())
                _existingHosts.Add((row.HostName.ToLowerInvariant(), row.Protocol));
        }

        OnPropertyChanged(nameof(SelectedCount));
        Log.Information("Imported {Count} host(s) from network scan into folder {Folder}", selected.Count, folderName);

        if (ImportCompleted is not null)
            await ImportCompleted.Invoke();
    }

    public void CancelIfRunning()
    {
        _cts?.Cancel();
        _batchTimer?.Stop();
    }

    internal void NotifySelectionChanged() => OnPropertyChanged(nameof(SelectedCount));

    private void DrainPendingResults()
    {
        while (_pendingResults.TryDequeue(out var row))
        {
            Results.Add(row);
        }
        OnPropertyChanged(nameof(SelectedCount));
    }

    private async Task LoadExistingHostsAsync()
    {
        var allEntries = await _repository.GetAllAsync();
        _existingHosts = [];
        foreach (var entry in allEntries.OfType<ConnectionEntry>())
        {
            if (!string.IsNullOrWhiteSpace(entry.HostName))
                _existingHosts.Add((entry.HostName.ToLowerInvariant(), entry.ConnectionType));
        }
    }

    private bool CheckHostExists(string ip, string hostName, ConnectionType protocol)
    {
        if (_existingHosts.Contains((ip.ToLowerInvariant(), protocol)))
            return true;
        if (_existingHosts.Contains((hostName.ToLowerInvariant(), protocol)))
            return true;
        // FQDN to short name
        var dot = hostName.IndexOf('.');
        if (dot > 0 && _existingHosts.Contains((hostName[..dot].ToLowerInvariant(), protocol)))
            return true;
        return false;
    }

    public partial class ScanResultRow : ObservableObject
    {
        private readonly NetworkScanViewModel _parent;

        public IPAddress IpAddress { get; }
        public string HostName { get; }
        public List<int> OpenPorts { get; }
        public ConnectionType Protocol { get; }
        public string ProtocolDisplay => Protocol.ToString();

        [ObservableProperty]
        private bool _isChecked;

        [ObservableProperty]
        private bool _existsInDatabase;

        [ObservableProperty]
        private int _selectedPort;

        public string StatusText => ExistsInDatabase ? "Exists" : "New";
        public bool CanCheck => !ExistsInDatabase;

        partial void OnIsCheckedChanged(bool value) => _parent.NotifySelectionChanged();

        partial void OnExistsInDatabaseChanged(bool value)
        {
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanCheck));
        }

        public ScanResultRow(IPAddress ip, string hostName, List<int> openPorts, bool exists, ConnectionType protocol, NetworkScanViewModel parent)
        {
            _parent = parent;
            IpAddress = ip;
            HostName = hostName;
            OpenPorts = openPorts;
            ExistsInDatabase = exists;
            Protocol = protocol;
            SelectedPort = openPorts.First();
        }
    }

    public record FolderOption(Guid? Id, string Name)
    {
        public override string ToString() => Name;
    }
}
