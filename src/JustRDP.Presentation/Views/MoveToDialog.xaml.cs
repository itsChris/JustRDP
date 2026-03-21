using System.Collections.ObjectModel;
using System.Windows;
using JustRDP.Presentation.ViewModels;

namespace JustRDP.Presentation.Views;

public partial class MoveToDialog : Window
{
    public Guid? SelectedFolderId { get; private set; }

    public MoveToDialog(ObservableCollection<TreeEntryViewModel> rootEntries, Guid excludeId)
    {
        InitializeComponent();

        var root = new MoveToItem { Name = "(Root)", FolderId = null, IconKind = "FolderHome" };
        BuildFolderTree(rootEntries, root, excludeId);

        FolderTree.ItemsSource = new[] { root };
    }

    private static void BuildFolderTree(ObservableCollection<TreeEntryViewModel> entries, MoveToItem parent, Guid excludeId)
    {
        foreach (var entry in entries)
        {
            if (entry.IsDashboard || entry.Id == excludeId || !entry.IsFolder) continue;

            var item = new MoveToItem
            {
                Name = entry.Name,
                FolderId = entry.Id,
                IconKind = "Folder"
            };
            parent.Children.Add(item);
            BuildFolderTree(entry.Children, item, excludeId);
        }
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        MoveButton.IsEnabled = e.NewValue is MoveToItem;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (FolderTree.SelectedItem is MoveToItem item)
        {
            SelectedFolderId = item.FolderId;
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

public class MoveToItem
{
    public string Name { get; set; } = "";
    public Guid? FolderId { get; set; }
    public string IconKind { get; set; } = "Folder";
    public ObservableCollection<MoveToItem> Children { get; } = [];
}
