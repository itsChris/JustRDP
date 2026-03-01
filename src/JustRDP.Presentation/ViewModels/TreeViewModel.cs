using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustRDP.Application.Services;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;

namespace JustRDP.Presentation.ViewModels;

public partial class TreeViewModel : ObservableObject
{
    private readonly TreeService _treeService;
    private readonly Action<ConnectionEntry>? _onConnectionDoubleClick;
    private readonly Func<ConnectionEntry, Task>? _onOpenConnection;
    private readonly Action<TreeEntryViewModel?>? _onSelectionChanged;

    [ObservableProperty]
    private TreeEntryViewModel? _selectedEntry;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntries))]
    private int _entryCount;

    public bool HasEntries => EntryCount > 0;

    public ObservableCollection<TreeEntryViewModel> RootEntries { get; } = [];

    public TreeViewModel(
        TreeService treeService,
        Action<ConnectionEntry>? onConnectionDoubleClick = null,
        Action<TreeEntryViewModel?>? onSelectionChanged = null,
        Func<ConnectionEntry, Task>? onOpenConnection = null)
    {
        _treeService = treeService;
        _onConnectionDoubleClick = onConnectionDoubleClick;
        _onSelectionChanged = onSelectionChanged;
        _onOpenConnection = onOpenConnection;
    }

    partial void OnSelectedEntryChanged(TreeEntryViewModel? value)
    {
        _onSelectionChanged?.Invoke(value);
    }

    public async Task LoadTreeAsync()
    {
        var allEntries = await _treeService.GetAllEntriesAsync();
        var lookup = allEntries.ToDictionary(e => e.Id);
        var viewModels = allEntries.ToDictionary(e => e.Id, e => new TreeEntryViewModel(e));

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
    }

    [RelayCommand]
    private async Task AddFolder(Guid? parentId)
    {
        var folder = await _treeService.CreateFolderAsync("New Folder", parentId);
        var vm = new TreeEntryViewModel(folder);
        AddToTree(vm, parentId);
        vm.BeginEditCommand.Execute(null);
        SelectedEntry = vm;
        EntryCount++;
    }

    [RelayCommand]
    private async Task AddConnection(Guid? parentId)
    {
        var connection = await _treeService.CreateConnectionAsync("New Connection", string.Empty, parentId);
        var vm = new TreeEntryViewModel(connection);
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
    private async Task DuplicateConnection(TreeEntryViewModel? entry)
    {
        if (entry?.Entity is not ConnectionEntry source) return;
        var duplicate = await _treeService.DuplicateConnectionAsync(source);
        var vm = new TreeEntryViewModel(duplicate);
        AddToTree(vm, duplicate.ParentId);
        vm.BeginEditCommand.Execute(null);
        SelectedEntry = vm;
        EntryCount++;
    }

    [RelayCommand]
    private async Task DeleteEntry(TreeEntryViewModel? entry)
    {
        if (entry is null) return;
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
        if (entry?.Entity is ConnectionEntry connection)
        {
            _onConnectionDoubleClick?.Invoke(connection);
        }
        else if (entry is not null)
        {
            entry.IsExpanded = !entry.IsExpanded;
        }
    }

    public async Task MoveEntryAsync(TreeEntryViewModel entry, Guid? newParentId, int newSortOrder)
    {
        // Remove from old location
        RemoveFromTree(entry);

        // Update entity
        entry.ParentId = newParentId;
        entry.SortOrder = newSortOrder;
        entry.Entity.ParentId = newParentId;
        entry.Entity.SortOrder = newSortOrder;

        // Add to new location
        AddToTree(entry, newParentId);

        // Persist
        await _treeService.UpdateAsync(entry.Entity);

        // Update sort orders for siblings
        var siblings = GetSiblings(newParentId);
        var updates = siblings.Select((s, i) => (s.Id, i)).ToList();
        await _treeService.UpdateSortOrderAsync(updates);
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
                return;
            }
        }
        RootEntries.Add(vm);
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
            count++;
            CountRecursive(entry.Children, ref count);
        }
    }
}
