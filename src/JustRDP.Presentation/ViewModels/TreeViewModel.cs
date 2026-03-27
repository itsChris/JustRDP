using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustRDP.Application.Services;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;
using JustRDP.Presentation.Behaviors;

namespace JustRDP.Presentation.ViewModels;

public partial class TreeViewModel : ObservableObject
{
    private readonly TreeService _treeService;
    private readonly Action<ConnectionEntry>? _onConnectionDoubleClick;
    private readonly Func<ConnectionEntry, Task>? _onOpenConnection;
    private readonly Action<TreeEntryViewModel?>? _onSelectionChanged;
    private Action? _onCheckedChanged;

    [ObservableProperty]
    private TreeEntryViewModel? _selectedEntry;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntries))]
    private int _entryCount;

    public bool HasEntries => EntryCount > 0 || DashboardNode is not null;

    public TreeEntryViewModel? DashboardNode { get; private set; }
    public ObservableCollection<TreeEntryViewModel> RootEntries { get; } = [];
    public ObservableCollection<TreeEntryViewModel> FilteredRootEntries { get; } = [];

    public TreeViewModel(
        TreeService treeService,
        Action<ConnectionEntry>? onConnectionDoubleClick = null,
        Action<TreeEntryViewModel?>? onSelectionChanged = null,
        Func<ConnectionEntry, Task>? onOpenConnection = null,
        Action? onCheckedChanged = null)
    {
        _treeService = treeService;
        _onConnectionDoubleClick = onConnectionDoubleClick;
        _onSelectionChanged = onSelectionChanged;
        _onOpenConnection = onOpenConnection;
        _onCheckedChanged = onCheckedChanged;
    }

    partial void OnSelectedEntryChanged(TreeEntryViewModel? value)
    {
        _onSelectionChanged?.Invoke(value);
    }

    public async Task LoadTreeAsync()
    {
        var allEntries = await _treeService.GetAllEntriesAsync();
        var lookup = allEntries.ToDictionary(e => e.Id);
        var viewModels = allEntries.ToDictionary(e => e.Id, e =>
        {
            var vm = new TreeEntryViewModel(e);
            vm.CheckedChanged = _onCheckedChanged;
            return vm;
        });

        RootEntries.Clear();

        foreach (var entry in allEntries.OrderBy(e => e.SortOrder))
        {
            var vm = viewModels[entry.Id];
            if (entry.ParentId.HasValue && viewModels.TryGetValue(entry.ParentId.Value, out var parentVm))
            {
                parentVm.Children.Add(vm);
            }
            else
            {
                RootEntries.Add(vm);
            }
        }

        EntryCount = allEntries.Count;
        SyncFilteredEntries();

        DashboardNode = new TreeEntryViewModel("Dashboard", isDashboard: true);
        RootEntries.Insert(0, DashboardNode);
        FilteredRootEntries.Insert(0, DashboardNode);
        SelectedEntry = DashboardNode;
    }

    public void ApplyFilter(string? filterText)
    {
        _currentFilter = filterText?.Trim() ?? string.Empty;
        ApplyFilterRecursive(RootEntries, _currentFilter);
        SyncFilteredEntries();
    }

    private string _currentFilter = string.Empty;

    private static bool ApplyFilterRecursive(ObservableCollection<TreeEntryViewModel> entries, string filter)
    {
        var anyVisible = false;
        foreach (var entry in entries)
        {
            if (entry.IsDashboard) { entry.IsVisible = true; anyVisible = true; continue; }
            var childVisible = ApplyFilterRecursive(entry.Children, filter);
            var nameMatch = string.IsNullOrEmpty(filter) ||
                            entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
            entry.IsVisible = nameMatch || childVisible;

            if (entry.IsVisible)
                anyVisible = true;

            // Auto-expand folders that have matching children during filter
            if (childVisible && !string.IsNullOrEmpty(filter) && entry.IsFolder)
                entry.IsExpanded = true;

            // Sync filtered children
            entry.FilteredChildren.Clear();
            foreach (var child in entry.Children)
            {
                if (child.IsVisible)
                    entry.FilteredChildren.Add(child);
            }
        }
        return anyVisible;
    }

    private void SyncFilteredEntries()
    {
        // If no filter active, sync all; otherwise only visible
        ApplyFilterRecursive(RootEntries, _currentFilter);
        FilteredRootEntries.Clear();
        foreach (var entry in RootEntries)
        {
            if (entry.IsVisible)
                FilteredRootEntries.Add(entry);
        }
    }

    public List<ConnectionEntry> GetCheckedConnections()
    {
        var results = new List<ConnectionEntry>();
        CollectCheckedConnections(RootEntries, results);
        return results;
    }

    private static void CollectCheckedConnections(ObservableCollection<TreeEntryViewModel> entries, List<ConnectionEntry> results)
    {
        foreach (var entry in entries)
        {
            if (entry.IsDashboard) continue;
            if (entry.IsChecked && entry.Entity is ConnectionEntry conn && !string.IsNullOrEmpty(conn.HostName))
                results.Add(conn);
            CollectCheckedConnections(entry.Children, results);
        }
    }

    public void ClearChecked()
    {
        ClearCheckedRecursive(RootEntries);
    }

    private static void ClearCheckedRecursive(ObservableCollection<TreeEntryViewModel> entries)
    {
        foreach (var entry in entries)
        {
            entry.IsChecked = false;
            ClearCheckedRecursive(entry.Children);
        }
    }

    private TreeEntryViewModel CreateVm(TreeEntry entity)
    {
        var vm = new TreeEntryViewModel(entity);
        vm.CheckedChanged = _onCheckedChanged;
        return vm;
    }

    [RelayCommand]
    private async Task AddFolder(Guid? parentId)
    {
        var folder = await _treeService.CreateFolderAsync("New Folder", parentId);
        var vm = CreateVm(folder);
        AddToTree(vm, parentId);
        vm.BeginEditCommand.Execute(null);
        SelectedEntry = vm;
        EntryCount++;
    }

    [RelayCommand]
    private async Task AddConnection(Guid? parentId)
    {
        var connection = await _treeService.CreateConnectionAsync("New Connection", string.Empty, parentId);
        var vm = CreateVm(connection);
        AddToTree(vm, parentId);
        vm.BeginEditCommand.Execute(null);
        SelectedEntry = vm;
        EntryCount++;
    }

    [RelayCommand]
    private async Task ConnectAll(TreeEntryViewModel? entry)
    {
        if (entry is null || _onOpenConnection is null) return;
        var connections = new List<ConnectionEntry>();
        CollectConnections(entry, connections);
        foreach (var connection in connections)
        {
            await _onOpenConnection(connection);
        }
    }

    private static void CollectConnections(TreeEntryViewModel entry, List<ConnectionEntry> results)
    {
        if (entry.Entity is ConnectionEntry conn && !string.IsNullOrEmpty(conn.HostName))
        {
            results.Add(conn);
        }
        foreach (var child in entry.Children)
        {
            CollectConnections(child, results);
        }
    }

    [RelayCommand]
    private async Task SortChildren(TreeEntryViewModel? entry)
    {
        if (entry is null || !entry.IsFolder || entry.IsDashboard) return;

        var sorted = entry.Children.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        entry.Children.Clear();
        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].SortOrder = i;
            sorted[i].Entity.SortOrder = i;
            entry.Children.Add(sorted[i]);
        }

        var updates = sorted.Select((s, i) => (s.Id, i)).ToList();
        await _treeService.UpdateSortOrderAsync(updates);
    }

    [RelayCommand]
    private async Task DuplicateConnection(TreeEntryViewModel? entry)
    {
        if (entry?.Entity is not ConnectionEntry source) return;
        var duplicate = await _treeService.DuplicateConnectionAsync(source);
        var vm = CreateVm(duplicate);
        AddToTree(vm, duplicate.ParentId);
        vm.BeginEditCommand.Execute(null);
        SelectedEntry = vm;
        EntryCount++;
    }

    [RelayCommand]
    private async Task DeleteEntry(TreeEntryViewModel? entry)
    {
        if (entry is null || entry.IsDashboard) return;
        await _treeService.DeleteAsync(entry.Id);
        RemoveFromTree(entry);
        if (SelectedEntry == entry)
            SelectedEntry = null;
        EntryCount = CountAll();
    }

    [RelayCommand]
    private async Task RenameEntry(TreeEntryViewModel? entry)
    {
        if (entry is null) return;
        var newName = entry.CommitEdit();
        if (newName is not null)
        {
            await _treeService.RenameAsync(entry.Id, newName);
        }
    }

    [RelayCommand]
    private void DoubleClickEntry(TreeEntryViewModel? entry)
    {
        if (entry is null || entry.IsDashboard) return;
        if (entry.Entity is ConnectionEntry connection)
        {
            _onConnectionDoubleClick?.Invoke(connection);
        }
        else
        {
            entry.IsExpanded = !entry.IsExpanded;
        }
    }

    public async Task MoveEntryToPositionAsync(TreeEntryViewModel entry, TreeEntryViewModel? target, DropPosition position)
    {
        Guid? newParentId;
        if (target is null)
            newParentId = null;
        else if (position == DropPosition.Into)
            newParentId = target.Id;
        else
            newParentId = target.ParentId;

        await MoveEntryInternalAsync(entry, newParentId, () =>
        {
            if (target is null || position == DropPosition.Into)
                return GetSiblings(newParentId).Count; // append at end

            var siblings = GetSiblings(newParentId);
            var targetIndex = siblings.IndexOf(target);
            if (targetIndex < 0) return siblings.Count;
            return position == DropPosition.Before ? targetIndex : targetIndex + 1;
        });
    }

    public async Task MoveEntryToFolderAsync(TreeEntryViewModel entry, Guid? folderId)
    {
        await MoveEntryInternalAsync(entry, folderId, () => GetSiblings(folderId).Count);
    }

    private async Task MoveEntryInternalAsync(TreeEntryViewModel entry, Guid? newParentId, Func<int> getInsertIndex)
    {
        var oldParentId = entry.ParentId;
        RemoveFromTree(entry);

        // Re-sequence old siblings so there are no gaps
        if (oldParentId != newParentId)
        {
            var oldSiblings = GetSiblings(oldParentId);
            UpdateSortOrdersInMemory(oldSiblings);
            await _treeService.UpdateSortOrderAsync(oldSiblings.Where(s => !s.IsDashboard).Select((s, i) => (s.Id, i)).ToList());
        }

        // Update entity
        entry.ParentId = newParentId;
        entry.Entity.ParentId = newParentId;

        // Insert at calculated position (computed AFTER removal so indices are correct)
        var newSiblings = GetSiblings(newParentId);
        var insertIndex = Math.Min(getInsertIndex(), newSiblings.Count);
        newSiblings.Insert(insertIndex, entry);

        if (newParentId.HasValue)
        {
            var parent = FindEntry(newParentId.Value, RootEntries);
            if (parent is not null)
                parent.IsExpanded = true;
        }

        // Re-sequence all siblings in new parent
        UpdateSortOrdersInMemory(newSiblings);

        // Persist
        await _treeService.UpdateAsync(entry.Entity);
        await _treeService.UpdateSortOrderAsync(newSiblings.Where(s => !s.IsDashboard).Select((s, i) => (s.Id, i)).ToList());

        SyncFilteredEntries();
    }

    private static void UpdateSortOrdersInMemory(ObservableCollection<TreeEntryViewModel> siblings)
    {
        for (var i = 0; i < siblings.Count; i++)
        {
            if (siblings[i].IsDashboard) continue;
            siblings[i].SortOrder = i;
            siblings[i].Entity.SortOrder = i;
        }
    }

    public async Task SaveExpandedStateAsync(TreeEntryViewModel entry)
    {
        if (entry.Entity is FolderEntry folder)
        {
            folder.IsExpanded = entry.IsExpanded;
            await _treeService.UpdateAsync(folder);
        }
    }

    private void AddToTree(TreeEntryViewModel vm, Guid? parentId)
    {
        if (parentId.HasValue)
        {
            var parent = FindEntry(parentId.Value, RootEntries);
            if (parent is not null)
            {
                parent.Children.Add(vm);
                parent.IsExpanded = true;
                SyncFilteredEntries();
                return;
            }
        }
        RootEntries.Add(vm);
        SyncFilteredEntries();
    }

    private void RemoveFromTree(TreeEntryViewModel entry)
    {
        if (!RemoveFromCollection(entry, RootEntries))
        {
            foreach (var root in RootEntries)
            {
                if (RemoveFromDescendants(entry, root))
                    break;
            }
        }
        SyncFilteredEntries();
    }

    private static bool RemoveFromCollection(TreeEntryViewModel entry, ObservableCollection<TreeEntryViewModel> collection)
    {
        return collection.Remove(entry);
    }

    private static bool RemoveFromDescendants(TreeEntryViewModel entry, TreeEntryViewModel parent)
    {
        if (parent.Children.Remove(entry))
            return true;

        foreach (var child in parent.Children)
        {
            if (RemoveFromDescendants(entry, child))
                return true;
        }
        return false;
    }

    private TreeEntryViewModel? FindEntry(Guid id, ObservableCollection<TreeEntryViewModel> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Id == id) return entry;
            var found = FindEntry(id, entry.Children);
            if (found is not null) return found;
        }
        return null;
    }

    private ObservableCollection<TreeEntryViewModel> GetSiblings(Guid? parentId)
    {
        if (parentId.HasValue)
        {
            var parent = FindEntry(parentId.Value, RootEntries);
            return parent?.Children ?? RootEntries;
        }
        return RootEntries;
    }

    private int CountAll()
    {
        int count = 0;
        CountRecursive(RootEntries, ref count);
        return count;
    }

    private static void CountRecursive(ObservableCollection<TreeEntryViewModel> entries, ref int count)
    {
        foreach (var entry in entries)
        {
            if (entry.IsDashboard) continue;
            count++;
            CountRecursive(entry.Children, ref count);
        }
    }
}
