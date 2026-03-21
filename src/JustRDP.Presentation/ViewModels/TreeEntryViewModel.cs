using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;

namespace JustRDP.Presentation.ViewModels;

public partial class TreeEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private bool _isChecked;

    partial void OnIsCheckedChanged(bool value)
    {
        CheckedChanged?.Invoke();
    }

    /// <summary>Raised when any entry's IsChecked changes. Wired by TreeViewModel.</summary>
    public Action? CheckedChanged { get; set; }

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private AvailabilityStatus _availabilityStatus = AvailabilityStatus.Unknown;

    public Guid Id { get; private set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public TreeEntryType EntryType { get; private set; }
    public Domain.Enums.ConnectionType? ConnectionType { get; private set; }
    public bool IsConnection => EntryType == TreeEntryType.Connection;
    public bool IsFolder => EntryType == TreeEntryType.Folder;
    public bool IsDashboard { get; init; }
    public bool ShowCheckBox => IsConnection && !IsDashboard;
    public bool ShowAvailabilityDot => IsConnection && !IsDashboard;
    public TreeEntry Entity { get; private set; }
    public ObservableCollection<TreeEntryViewModel> Children { get; } = [];
    public ObservableCollection<TreeEntryViewModel> FilteredChildren { get; } = [];

    public TreeEntryViewModel(TreeEntry entity)
    {
        Entity = entity;
        Id = entity.Id;
        Name = entity.Name;
        ParentId = entity.ParentId;
        SortOrder = entity.SortOrder;

        if (entity is FolderEntry folder)
        {
            EntryType = TreeEntryType.Folder;
            IsExpanded = folder.IsExpanded;
        }
        else
        {
            EntryType = TreeEntryType.Connection;
            ConnectionType = entity is ConnectionEntry conn ? conn.ConnectionType : null;
        }
    }

    public TreeEntryViewModel(string name, bool isDashboard)
    {
        IsDashboard = isDashboard;
        Entity = null!;
        Id = Guid.Empty;
        Name = name;
        ParentId = null;
        SortOrder = -1;
        EntryType = TreeEntryType.Folder;
        ConnectionType = null;
    }

    [RelayCommand]
    private void BeginEdit()
    {
        EditName = Name;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
    }

    public string? CommitEdit()
    {
        IsEditing = false;
        var trimmed = EditName.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == Name)
            return null;
        Name = trimmed;
        return trimmed;
    }
}
