using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustRDP.Application.Services;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Interfaces;
using JustRDP.Presentation.Themes;
using JustRDP.Presentation.Views;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoTabs))]
    private int _openTabCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSelection))]
    private TreeEntryViewModel? _selectedEntry;

    [ObservableProperty]
    private ConnectionTabViewModel? _selectedTab;

    partial void OnSelectedTabChanged(ConnectionTabViewModel? oldValue, ConnectionTabViewModel? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
    }

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isDarkTheme = true;

    public bool HasNoTabs => OpenTabCount == 0;
    public bool HasNoSelection => SelectedEntry is null;

    public TreeViewModel TreeVM { get; }
    public PropertiesViewModel PropertiesVM { get; } = new();
    public ObservableCollection<ConnectionTabViewModel> OpenTabs { get; } = [];

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        TreeService treeService,
        CredentialInheritanceService credentialService,
        ImportExportService importExportService,
        ICredentialEncryptor encryptor,
        ThemeManager themeManager)
    {
        _logger = logger;
        _treeService = treeService;
        _credentialService = credentialService;
        _importExportService = importExportService;
        _encryptor = encryptor;
        _themeManager = themeManager;
        TreeVM = new TreeViewModel(treeService, OnConnectionDoubleClick, OnSelectionChanged, OpenConnectionAsync);
    }

    public async Task InitializeAsync()
    {
        IsDarkTheme = await _themeManager.LoadThemeAsync();
        await TreeVM.LoadTreeAsync();
        UpdateStatus();
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
        var tabVm = new ConnectionTabViewModel(connection, credential);
        tabVm.CloseRequested += tab => CloseTab(tab);
        OpenTabs.Add(tabVm);
        SelectedTab = tabVm;
        OpenTabCount = OpenTabs.Count;
        UpdateStatus();
    }

    [RelayCommand]
    private void CloseTab(ConnectionTabViewModel? tab)
    {
        if (tab is null) return;
        _logger.LogInformation("Closing connection tab {Name}", tab.TabTitle);
        tab.Disconnect();
        OpenTabs.Remove(tab);
        OpenTabCount = OpenTabs.Count;
        UpdateStatus();
    }

    private async void OnSelectionChanged(TreeEntryViewModel? entry)
    {
        SelectedEntry = entry;
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
    }

    [RelayCommand]
    private async Task AddConnection()
    {
        var parentId = SelectedEntry?.EntryType == Domain.Enums.TreeEntryType.Folder
            ? SelectedEntry.Id
            : SelectedEntry?.ParentId;
        await TreeVM.AddConnectionCommand.ExecuteAsync(parentId);
        UpdateStatus();
    }

    [RelayCommand]
    private void RenameSelected()
    {
        SelectedEntry?.BeginEditCommand.Execute(null);
    }

    [RelayCommand]
    private async Task DeleteSelected()
    {
        if (SelectedEntry is null) return;
        await TreeVM.DeleteEntryCommand.ExecuteAsync(SelectedEntry);
        UpdateStatus();
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
        if (SelectedEntry is null) return;

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

    private void UpdateStatus()
    {
        var count = TreeVM.HasEntries ? $"{TreeVM.EntryCount} entries" : "No entries";
        StatusText = OpenTabCount > 0
            ? $"{count} | {OpenTabCount} connection(s) open"
            : count;
    }
}
