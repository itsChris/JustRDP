using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustRDP.Application.Services;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;
using JustRDP.Domain.Interfaces;
using JustRDP.Domain.ValueObjects;
using JustRDP.Presentation.Services;
using JustRDP.Presentation.Themes;
using JustRDP.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JustRDP.Presentation.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly TreeService _treeService;
    private readonly CredentialInheritanceService _credentialService;
    private readonly ImportExportService _importExportService;
    private readonly ICredentialEncryptor _encryptor;
    private readonly ThemeManager _themeManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly AvailabilityMonitorService _availabilityMonitor;
    private NetworkScanWindow? _scanWindow;
    private IServiceScope? _scanScope;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoTabs))]
    private int _openTabCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSelection))]
    private TreeEntryViewModel? _selectedEntry;

    [ObservableProperty]
    private IConnectionTab? _selectedTab;

    partial void OnSelectedTabChanged(IConnectionTab? oldValue, IConnectionTab? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null)
        {
            newValue.IsSelected = true;
            IsDashboardVisible = false;
        }
    }

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private string _quickConnectAddress = string.Empty;

    [ObservableProperty]
    private string _treeFilterText = string.Empty;

    [ObservableProperty]
    private bool _hasCheckedEntries;

    [ObservableProperty]
    private bool _isMonitoringEnabled;

    [ObservableProperty]
    private bool _isDashboardVisible = true;

    public bool HasNoTabs => OpenTabCount == 0;
    public bool HasNoSelection => SelectedEntry is null;

    public DashboardViewModel DashboardVM { get; }

    partial void OnTreeFilterTextChanged(string value)
    {
        TreeVM.ApplyFilter(value);
    }

    public TreeViewModel TreeVM { get; }
    public PropertiesViewModel PropertiesVM { get; } = new();
    public ObservableCollection<IConnectionTab> OpenTabs { get; } = [];

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        TreeService treeService,
        CredentialInheritanceService credentialService,
        ImportExportService importExportService,
        ICredentialEncryptor encryptor,
        ThemeManager themeManager,
        IServiceProvider serviceProvider,
        AvailabilityMonitorService availabilityMonitor)
    {
        _logger = logger;
        _treeService = treeService;
        _credentialService = credentialService;
        _importExportService = importExportService;
        _encryptor = encryptor;
        _themeManager = themeManager;
        _serviceProvider = serviceProvider;
        _availabilityMonitor = availabilityMonitor;
        _availabilityMonitor.SummaryChanged += OnAvailabilitySummaryChanged;
        TreeVM = new TreeViewModel(treeService, OnConnectionDoubleClick, OnSelectionChanged, OpenConnectionAsync,
            () => HasCheckedEntries = TreeVM!.GetCheckedConnections().Count > 0);
        DashboardVM = new DashboardViewModel(OpenConnectionAsync, ConnectionExistsInTree, RefreshDashboard);
    }

    public async Task InitializeAsync()
    {
        IsDarkTheme = await _themeManager.LoadThemeAsync();
        await TreeVM.LoadTreeAsync();
        IsMonitoringEnabled = await _availabilityMonitor.LoadEnabledStateAsync();
        _availabilityMonitor.Initialize(TreeVM);
        UpdateStatus();
        RefreshDashboard();
    }

    private async void OnConnectionDoubleClick(ConnectionEntry connection)
    {
        await OpenConnectionAsync(connection);
    }

    public async Task OpenConnectionAsync(ConnectionEntry connection)
    {
        var existing = OpenTabs.FirstOrDefault(t => t.ConnectionId == connection.Id);
        if (existing is not null)
        {
            _logger.LogDebug("Connection {Name} already open, switching tab", connection.Name);
            SelectedTab = existing;
            return;
        }

        _logger.LogInformation("Opening connection {Name} ({Host}:{Port})", connection.Name, connection.HostName, connection.Port);
        var credential = await _credentialService.ResolveCredentialAsync(connection);

        IConnectionTab tabVm;
        if (connection.ConnectionType == ConnectionType.SSH)
        {
            _logger.LogInformation("Opening SSH session to {Host}:{Port} (user={User}, hasKey={HasKey})",
                connection.HostName, connection.Port,
                string.IsNullOrWhiteSpace(credential.Username) ? "<none>" : credential.Username,
                !string.IsNullOrEmpty(connection.SshPrivateKeyPath));
            tabVm = new SshTabViewModel(connection, credential, _encryptor, _serviceProvider.GetService<ILoggerFactory>());
        }
        else
        {
            tabVm = new ConnectionTabViewModel(connection, credential);
        }

        tabVm.CloseRequested += tab => CloseTab(tab);
        OpenTabs.Add(tabVm);
        SelectedTab = tabVm;
        OpenTabCount = OpenTabs.Count;

        // Track usage for persisted connections (skip quick connect)
        if (connection.Id != Guid.Empty)
        {
            connection.LastConnectedAt = DateTime.UtcNow;
            connection.ConnectCount++;
            _ = Task.Run(async () =>
            {
                try { await _treeService.UpdateUsageAsync(connection.Id, connection.LastConnectedAt.Value, connection.ConnectCount); }
                catch (Exception ex) { _logger.LogWarning(ex, "Usage tracking failed for {Name}", connection.Name); }
            });
        }

        UpdateStatus();
        RefreshDashboard();
    }

    [RelayCommand]
    private void CloseTab(IConnectionTab? tab)
    {
        if (tab is null) return;
        _logger.LogInformation("Closing connection tab {Name}", tab.TabTitle);
        tab.Disconnect();
        OpenTabs.Remove(tab);
        OpenTabCount = OpenTabs.Count;
        if (OpenTabs.Count == 0)
        {
            IsDashboardVisible = true;
            RefreshDashboard();
        }
        UpdateStatus();
    }

    private async void OnSelectionChanged(TreeEntryViewModel? entry)
    {
        SelectedEntry = entry;
        if (entry?.IsDashboard == true)
        {
            IsDashboardVisible = true;
            RefreshDashboard();
            return;
        }
        await UpdatePropertiesPanelAsync(entry);
    }

    private async Task UpdatePropertiesPanelAsync(TreeEntryViewModel? entry)
    {
        if (entry?.Entity is ConnectionEntry conn)
        {
            var credential = await _credentialService.ResolveCredentialAsync(conn);
            PropertiesVM.LoadEntry(conn, credential);
        }
        else
        {
            PropertiesVM.LoadEntry(entry?.Entity);
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        var logFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JustRDP", "logs");
        Directory.CreateDirectory(logFolder);
        System.Diagnostics.Process.Start("explorer.exe", logFolder);
    }

    [RelayCommand]
    private async Task QuickConnect()
    {
        var address = QuickConnectAddress?.Trim();
        if (string.IsNullOrEmpty(address)) return;

        // SSH quick connect: ssh://[user@]host[:port]
        if (address.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = address[6..];
            string? user = null;
            if (uri.Contains('@'))
            {
                var p = uri.Split('@', 2);
                user = p[0];
                uri = p[1];
            }
            var hp = uri.Split(':', 2);
            var host = hp[0];
            var port = hp.Length > 1 && int.TryParse(hp[1], out var p2) ? p2 : 22;

            _logger.LogInformation("Quick SSH connect to {Host}:{Port}", host, port);

            var conn = new ConnectionEntry
            {
                Name = address,
                HostName = host,
                Port = port,
                ConnectionType = ConnectionType.SSH,
                CredentialUsername = user
            };
            var cred = new Credential(user ?? "", "", "", null);
            var tabVm = new SshTabViewModel(conn, cred, _encryptor, _serviceProvider.GetService<ILoggerFactory>());
            tabVm.CloseRequested += tab => CloseTab(tab);
            OpenTabs.Add(tabVm);
            SelectedTab = tabVm;
            OpenTabCount = OpenTabs.Count;
            UpdateStatus();
            QuickConnectAddress = string.Empty;
            return;
        }

        // RDP quick connect: host[:port]
        var parts = address.Split(':', 2);
        var rdpHost = parts[0];
        var rdpPort = parts.Length > 1 && int.TryParse(parts[1], out var pp) ? pp : 3389;

        _logger.LogInformation("Quick connect to {Host}:{Port}", rdpHost, rdpPort);

        var connection = new ConnectionEntry
        {
            Name = address,
            HostName = rdpHost,
            Port = rdpPort
        };

        var credential = new Credential(string.Empty, string.Empty, string.Empty, null);
        var rdpTab = new ConnectionTabViewModel(connection, credential);
        rdpTab.CloseRequested += tab => CloseTab(tab);
        OpenTabs.Add(rdpTab);
        SelectedTab = rdpTab;
        OpenTabCount = OpenTabs.Count;
        UpdateStatus();

        QuickConnectAddress = string.Empty;
    }

    [RelayCommand]
    private async Task ConnectSelected()
    {
        var connections = TreeVM.GetCheckedConnections();
        if (connections.Count == 0) return;

        _logger.LogInformation("Connecting to {Count} selected entries", connections.Count);
        foreach (var connection in connections)
        {
            await OpenConnectionAsync(connection);
        }
        TreeVM.ClearChecked();
        HasCheckedEntries = false;
    }

    [RelayCommand]
    private void ShowAbout()
    {
        var dialog = new Views.AboutDialog { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void ShowScan()
    {
        if (_scanWindow is not null && _scanWindow.IsLoaded)
        {
            _scanWindow.Activate();
            return;
        }

        _scanScope = _serviceProvider.CreateScope();
        _scanWindow = _scanScope.ServiceProvider.GetRequiredService<NetworkScanWindow>();

        // Wire up immediate tree refresh on import
        var scanVm = (NetworkScanViewModel)_scanWindow.DataContext;
        scanVm.ImportCompleted += async () =>
        {
            await TreeVM.LoadTreeAsync();
            UpdateStatus();
            RefreshDashboard();
        };

        _scanWindow.Closed += (_, _) =>
        {
            _scanWindow = null;
            _scanScope?.Dispose();
            _scanScope = null;
        };
        _scanWindow.Show();
    }

    [RelayCommand]
    private async Task ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        await _themeManager.SetThemeAsync(IsDarkTheme);
    }

    [RelayCommand]
    private void CloseActiveTab()
    {
        if (SelectedTab is null) return;
        SelectedTab.Disconnect();
        OpenTabs.Remove(SelectedTab);
        OpenTabCount = OpenTabs.Count;
        SelectedTab = OpenTabs.LastOrDefault();
        if (OpenTabs.Count == 0)
        {
            IsDashboardVisible = true;
            RefreshDashboard();
        }
        UpdateStatus();
    }

    [RelayCommand]
    private async Task AddFolder()
    {
        var parentId = SelectedEntry?.EntryType == Domain.Enums.TreeEntryType.Folder
            ? SelectedEntry.Id
            : SelectedEntry?.ParentId;
        await TreeVM.AddFolderCommand.ExecuteAsync(parentId);
        UpdateStatus();
        RefreshDashboard();
    }

    [RelayCommand]
    private async Task AddConnection()
    {
        var parentId = SelectedEntry?.EntryType == Domain.Enums.TreeEntryType.Folder
            ? SelectedEntry.Id
            : SelectedEntry?.ParentId;
        await TreeVM.AddConnectionCommand.ExecuteAsync(parentId);
        UpdateStatus();
        RefreshDashboard();
    }

    [RelayCommand]
    private void RenameSelected()
    {
        if (SelectedEntry is { IsDashboard: true }) return;
        SelectedEntry?.BeginEditCommand.Execute(null);
    }

    [RelayCommand]
    private async Task DeleteSelected()
    {
        if (SelectedEntry is null or { IsDashboard: true }) return;
        await TreeVM.DeleteEntryCommand.ExecuteAsync(SelectedEntry);
        UpdateStatus();
        RefreshDashboard();
    }

    public async Task CommitRenameAsync(TreeEntryViewModel vm)
    {
        await TreeVM.RenameEntryCommand.ExecuteAsync(vm);
        if (vm == SelectedEntry)
            await UpdatePropertiesPanelAsync(vm);
    }

    [RelayCommand]
    private void OpenPropertiesDialog()
    {
        if (SelectedEntry is null or { IsDashboard: true }) return;

        var propsVm = new ConnectionPropertiesViewModel(_treeService, _encryptor);

        if (SelectedEntry.Entity is ConnectionEntry conn)
            propsVm.LoadConnection(conn);
        else if (SelectedEntry.Entity is FolderEntry folder)
            propsVm.LoadFolder(folder);
        else
            return;

        var dialog = new PropertiesDialog(propsVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedEntry.Name = propsVm.Name;
            OnSelectionChanged(SelectedEntry);
        }
    }

    [RelayCommand]
    private async Task Import()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import Connections",
            Filter = "All Supported|*.json;*.rdp|JSON Files|*.json|RDP Files|*.rdp",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) return;

        _logger.LogInformation("Importing {Count} file(s)", dialog.FileNames.Length);
        foreach (var file in dialog.FileNames)
        {
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".rdp")
            {
                var entry = Infrastructure.Import.RdpFileParser.Parse(file);
                await _importExportService.ImportEntriesAsync([entry]);
            }
            else if (ext == ".json")
            {
                var entries = Infrastructure.Import.JsonTreeImporter.Import(file);
                await _importExportService.ImportEntriesAsync(entries);
            }
        }

        await TreeVM.LoadTreeAsync();
        UpdateStatus();
        RefreshDashboard();
        StatusText = $"Imported {dialog.FileNames.Length} file(s)";
    }

    [RelayCommand]
    private async Task Export()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Connections",
            Filter = "JSON File|*.json",
            DefaultExt = ".json",
            FileName = "justrdp-export"
        };

        if (dialog.ShowDialog() != true) return;

        var entries = await _importExportService.GetAllForExportAsync();
        Infrastructure.Export.JsonTreeExporter.Export(entries, dialog.FileName);
        _logger.LogInformation("Exported {Count} entries to {File}", entries.Count, dialog.FileName);
        StatusText = $"Exported to {System.IO.Path.GetFileName(dialog.FileName)}";
    }

    [RelayCommand]
    private async Task ToggleMonitoring()
    {
        // IsMonitoringEnabled already toggled by ToggleButton binding
        await _availabilityMonitor.SetEnabledAsync(IsMonitoringEnabled, TreeVM);
        UpdateStatus();
    }

    public void PauseMonitoring() => _availabilityMonitor.Pause();
    public void ResumeMonitoring() => _availabilityMonitor.Resume();

    private void OnAvailabilitySummaryChanged()
    {
        UpdateStatus();
        RefreshDashboard();
    }

    private void UpdateStatus()
    {
        var count = TreeVM.HasEntries ? $"{TreeVM.EntryCount} entries" : "No entries";
        var status = OpenTabCount > 0
            ? $"{count} | {OpenTabCount} connection(s) open"
            : count;

        if (IsMonitoringEnabled && _availabilityMonitor.TotalChecked > 0)
            status += $" | {_availabilityMonitor.AvailableCount}/{_availabilityMonitor.TotalChecked} available";

        StatusText = status;
    }

    private bool ConnectionExistsInTree(Guid connectionId)
    {
        return FindConnectionInTree(connectionId, TreeVM.RootEntries);
    }

    private static bool FindConnectionInTree(Guid id, ObservableCollection<TreeEntryViewModel> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.IsDashboard) continue;
            if (entry.Id == id) return true;
            if (FindConnectionInTree(id, entry.Children)) return true;
        }
        return false;
    }

    private void RefreshDashboard()
    {
        DashboardVM.Refresh(BuildDashboardData());
    }

    private DashboardData BuildDashboardData()
    {
        var allItems = new List<DashboardConnectionItem>();
        CollectDashboardConnections(TreeVM.RootEntries, allItems);

        var onlineCount = 0;
        var offlineCount = 0;
        var rdpCount = 0;
        var sshCount = 0;

        foreach (var item in allItems)
        {
            if (item.Status == AvailabilityStatus.Available) onlineCount++;
            else if (item.Status == AvailabilityStatus.Unavailable) offlineCount++;

            if (item.Protocol == "SSH") sshCount++;
            else rdpCount++;
        }

        var recentItems = allItems
            .Where(i => i.LastConnectedAt.HasValue)
            .OrderByDescending(i => i.LastConnectedAt)
            .Take(10)
            .ToList();

        return new DashboardData
        {
            TotalConnections = allItems.Count,
            OnlineCount = onlineCount,
            OfflineCount = offlineCount,
            OpenSessions = OpenTabs.Count,
            RdpCount = rdpCount,
            SshCount = sshCount,
            IsMonitoringEnabled = IsMonitoringEnabled,
            AllConnections = allItems,
            RecentConnections = recentItems
        };
    }

    private static void CollectDashboardConnections(
        System.Collections.ObjectModel.ObservableCollection<TreeEntryViewModel> entries,
        List<DashboardConnectionItem> results)
    {
        foreach (var entry in entries)
        {
            if (entry.IsDashboard) continue;
            if (entry.Entity is ConnectionEntry conn && !string.IsNullOrWhiteSpace(conn.HostName))
            {
                results.Add(new DashboardConnectionItem
                {
                    Connection = conn,
                    Name = conn.Name,
                    Host = conn.HostName,
                    Protocol = conn.ConnectionType == ConnectionType.SSH ? "SSH" : "RDP",
                    Port = conn.Port,
                    ConnectionType = conn.ConnectionType,
                    Status = entry.AvailabilityStatus,
                    LastConnectedAt = conn.LastConnectedAt
                });
            }
            CollectDashboardConnections(entry.Children, results);
        }
    }
}
