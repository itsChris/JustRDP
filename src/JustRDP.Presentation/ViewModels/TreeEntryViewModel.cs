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

    public Guid Id { get; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public TreeEntryType EntryType { get; }
    public Domain.Enums.ConnectionType? ConnectionType { get; }
    public bool IsConnection => EntryType == TreeEntryType.Connection;
    public bool IsFolder => EntryType == TreeEntryType.Folder;
    public TreeEntry Entity { get; }
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
