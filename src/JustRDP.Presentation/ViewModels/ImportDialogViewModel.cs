using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustRDP.Application.Services;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;
using JustRDP.Domain.Interfaces;
using JustRDP.Infrastructure.Import;
using Serilog;

namespace JustRDP.Presentation.ViewModels;

public partial class ImportDialogViewModel : ObservableObject
{
    private readonly TreeService _treeService;
    private readonly ITreeEntryRepository _repository;
    private readonly ImportExportService _importExportService;
    private HashSet<(string Host, ConnectionType Protocol)> _existingHosts = [];

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _resultsHeader = string.Empty;

    [ObservableProperty]
    private bool _hasItems;

    [ObservableProperty]
    private bool _hasNewItems;

    [ObservableProperty]
    private ImportFolderOption? _selectedFolder;

    [ObservableProperty]
    private string _selectedFilesText = string.Empty;

    [ObservableProperty]
    private bool _windowsEntriesLoaded;

    public ObservableCollection<ImportPreviewRow> PreviewItems { get; } = [];
    public ObservableCollection<ImportFolderOption> TargetFolders { get; } = [];

    public int SelectedCount => PreviewItems.Count(r => r.IsChecked);

    public event Func<Task>? ImportCompleted;

    public ImportDialogViewModel(
        TreeService treeService,
        ITreeEntryRepository repository,
        ImportExportService importExportService)
    {
        _treeService = treeService;
        _repository = repository;
        _importExportService = importExportService;
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == 1 && !_windowsEntriesLoaded)
        {
            LoadWindowsEntries();
        }
    }

    public async Task LoadFoldersAsync()
    {
        TargetFolders.Clear();
        TargetFolders.Add(new ImportFolderOption(null, "(Root)"));

        var entries = await _treeService.GetAllEntriesAsync();
        foreach (var entry in entries.OfType<FolderEntry>().OrderBy(e => e.Name))
        {
            TargetFolders.Add(new ImportFolderOption(entry.Id, entry.Name));
        }

        SelectedFolder = TargetFolders.FirstOrDefault();
    }

    public async Task LoadExistingHostsAsync()
    {
        var allEntries = await _repository.GetAllAsync();
        _existingHosts = [];
        foreach (var entry in allEntries.OfType<ConnectionEntry>())
        {
            if (!string.IsNullOrWhiteSpace(entry.HostName))
                _existingHosts.Add((entry.HostName.ToLowerInvariant(), entry.ConnectionType));
        }
    }

    [RelayCommand]
    private async Task BrowseFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Files to Import",
            Filter = "All Supported|*.json;*.rdp|JSON Files|*.json|RDP Files|*.rdp",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) return;

        await LoadExistingHostsAsync();
        PreviewItems.Clear();

        var fileEntries = new List<(TreeEntry Entry, string FileName)>();

        foreach (var file in dialog.FileNames)
        {
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".rdp")
            {
                var entry = RdpFileParser.Parse(file);
                fileEntries.Add((entry, System.IO.Path.GetFileName(file)));
            }
            else if (ext == ".json")
            {
                var entries = JsonTreeImporter.Import(file);
                foreach (var entry in entries)
                {
                    fileEntries.Add((entry, System.IO.Path.GetFileName(file)));
                }
            }
        }

        foreach (var (entry, fileName) in fileEntries)
        {
            if (entry is ConnectionEntry conn)
            {
                var exists = CheckHostExists(conn.HostName ?? "", conn.ConnectionType);
                PreviewItems.Add(new ImportPreviewRow(this)
                {
                    DisplayName = conn.Name,
                    HostName = conn.HostName ?? "",
                    Port = conn.Port,
                    Username = conn.CredentialUsername,
                    Domain = conn.CredentialDomain,
                    Protocol = conn.ConnectionType,
                    ExistsInDatabase = exists,
                    SourceEntry = conn,
                    SourceFile = fileName
                });
            }
            else if (entry is FolderEntry folder)
            {
                // Folders from JSON imports — add as-is for batch import
                PreviewItems.Add(new ImportPreviewRow(this)
                {
                    DisplayName = folder.Name,
                    HostName = "",
                    Port = 0,
                    Protocol = ConnectionType.RDP,
                    IsFolder = true,
                    ExistsInDatabase = false,
                    SourceFolder = folder,
                    SourceFile = fileName
                });
            }
        }

        SelectedFilesText = $"{dialog.FileNames.Length} file(s) selected";
        UpdateHeader();
    }

    [RelayCommand]
    private void LoadWindowsEntries()
    {
        PreviewItems.Clear();

        var mruEntries = MstscRegistryReader.ReadMruEntries();

        foreach (var mru in mruEntries)
        {
            // Parse DOMAIN\user format
            string? username = mru.Username;
            string? domain = null;
            if (username is not null && username.Contains('\\'))
            {
                var parts = username.Split('\\', 2);
                domain = parts[0];
                username = parts[1];
            }

            var exists = CheckHostExists(mru.HostName, ConnectionType.RDP);
            PreviewItems.Add(new ImportPreviewRow(this)
            {
                DisplayName = mru.HostName,
                HostName = mru.HostName,
                Port = mru.Port,
                Username = username,
                Domain = domain,
                Protocol = ConnectionType.RDP,
                ExistsInDatabase = exists
            });
        }

        WindowsEntriesLoaded = true;
        UpdateHeader();

        if (mruEntries.Count == 0)
            ResultsHeader = "No mstsc.exe connections found in the Windows registry.";
    }

    [RelayCommand]
    private void SelectAllNew()
    {
        foreach (var row in PreviewItems)
        {
            if (!row.ExistsInDatabase)
                row.IsChecked = true;
        }
        OnPropertyChanged(nameof(SelectedCount));
    }

    [RelayCommand]
    private async Task ImportSelected()
    {
        var selected = PreviewItems.Where(r => r.IsChecked && !r.ExistsInDatabase).ToList();
        if (selected.Count == 0) return;

        var parentId = SelectedFolder?.Id;
        var folderName = SelectedFolder?.Name ?? "(Root)";

        // Check if we have full entries from file import (with all settings preserved)
        var fileEntries = selected.Where(r => r.SourceEntry is not null || r.SourceFolder is not null).ToList();
        var registryEntries = selected.Where(r => r.SourceEntry is null && r.SourceFolder is null).ToList();

        // Import file-based entries via ImportExportService (preserves all parsed settings)
        if (fileEntries.Count > 0)
        {
            var entriesToImport = new List<TreeEntry>();
            foreach (var row in fileEntries)
            {
                if (row.SourceEntry is not null)
                {
                    row.SourceEntry.ParentId = parentId;
                    entriesToImport.Add(row.SourceEntry);
                }
                else if (row.SourceFolder is not null)
                {
                    row.SourceFolder.ParentId = parentId;
                    entriesToImport.Add(row.SourceFolder);
                }
            }
            await _importExportService.ImportEntriesAsync(entriesToImport);
        }

        // Import registry-based entries via TreeService (simple host+port+credentials)
        foreach (var row in registryEntries)
        {
            var entry = await _treeService.CreateConnectionAsync(
                row.DisplayName, row.HostName, parentId, row.Port, row.Protocol);

            // Set credentials if available from registry
            if (!string.IsNullOrEmpty(row.Username))
            {
                entry.CredentialUsername = row.Username;
                entry.CredentialDomain = row.Domain;
                await _repository.UpdateAsync(entry);
            }
        }

        // Mark imported rows as existing
        foreach (var row in selected)
        {
            row.ExistsInDatabase = true;
            row.IsChecked = false;
            if (!string.IsNullOrWhiteSpace(row.HostName))
                _existingHosts.Add((row.HostName.ToLowerInvariant(), row.Protocol));
        }

        OnPropertyChanged(nameof(SelectedCount));
        UpdateHeader();
        Log.Information("Imported {Count} entry/entries into folder {Folder}", selected.Count, folderName);

        if (ImportCompleted is not null)
            await ImportCompleted.Invoke();
    }

    internal void NotifySelectionChanged() => OnPropertyChanged(nameof(SelectedCount));

    private bool CheckHostExists(string hostName, ConnectionType protocol)
    {
        if (string.IsNullOrWhiteSpace(hostName)) return false;
        if (_existingHosts.Contains((hostName.ToLowerInvariant(), protocol)))
            return true;
        var dot = hostName.IndexOf('.');
        if (dot > 0 && _existingHosts.Contains((hostName[..dot].ToLowerInvariant(), protocol)))
            return true;
        return false;
    }

    private void UpdateHeader()
    {
        var total = PreviewItems.Count;
        var existing = PreviewItems.Count(r => r.ExistsInDatabase);
        HasItems = total > 0;
        HasNewItems = PreviewItems.Any(r => !r.ExistsInDatabase);

        if (total == 0)
            ResultsHeader = string.Empty;
        else if (existing > 0)
            ResultsHeader = $"{total} connection(s) found ({existing} already exist)";
        else
            ResultsHeader = $"{total} connection(s) found";
    }

    public partial class ImportPreviewRow : ObservableObject
    {
        private readonly ImportDialogViewModel _parent;

        public string DisplayName { get; init; } = "";
        public string HostName { get; init; } = "";
        public int Port { get; init; }
        public string? Username { get; init; }
        public string? Domain { get; init; }
        public ConnectionType Protocol { get; init; }
        public bool IsFolder { get; init; }
        public string ProtocolDisplay => IsFolder ? "Folder" : Protocol.ToString();
        public string UsernameDisplay => Username is not null
            ? (Domain is not null ? $"{Domain}\\{Username}" : Username)
            : "—";

        // Source data for file imports
        public ConnectionEntry? SourceEntry { get; init; }
        public FolderEntry? SourceFolder { get; init; }
        public string? SourceFile { get; init; }

        [ObservableProperty]
        private bool _isChecked;

        [ObservableProperty]
        private bool _existsInDatabase;

        public string StatusText => ExistsInDatabase ? "Exists" : "New";
        public bool CanCheck => !ExistsInDatabase;

        partial void OnIsCheckedChanged(bool value) => _parent.NotifySelectionChanged();

        partial void OnExistsInDatabaseChanged(bool value)
        {
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanCheck));
        }

        public ImportPreviewRow(ImportDialogViewModel parent)
        {
            _parent = parent;
        }
    }

    public record ImportFolderOption(Guid? Id, string Name)
    {
        public override string ToString() => Name;
    }
}
