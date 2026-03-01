using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using JustRDP.Presentation.ViewModels;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using DataObject = System.Windows.DataObject;
using DragDrop = System.Windows.DragDrop;
using Point = System.Windows.Point;
using TreeView = System.Windows.Controls.TreeView;
using TreeViewItem = System.Windows.Controls.TreeViewItem;

namespace JustRDP.Presentation.Behaviors;

public static class TreeViewDragDropBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(TreeViewDragDropBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty TreeViewModelProperty =
        DependencyProperty.RegisterAttached("TreeViewModel", typeof(TreeViewModel), typeof(TreeViewDragDropBehavior));

    private static Point _startPoint;
    private static TreeEntryViewModel? _draggedItem;

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static TreeViewModel? GetTreeViewModel(DependencyObject obj) => (TreeViewModel?)obj.GetValue(TreeViewModelProperty);
    public static void SetTreeViewModel(DependencyObject obj, TreeViewModel? value) => obj.SetValue(TreeViewModelProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TreeView treeView) return;

        if ((bool)e.NewValue)
        {
            treeView.PreviewMouseLeftButtonDown += TreeView_PreviewMouseLeftButtonDown;
            treeView.PreviewMouseMove += TreeView_PreviewMouseMove;
            treeView.Drop += TreeView_Drop;
            treeView.DragOver += TreeView_DragOver;
            treeView.AllowDrop = true;
        }
        else
        {
            treeView.PreviewMouseLeftButtonDown -= TreeView_PreviewMouseLeftButtonDown;
            treeView.PreviewMouseMove -= TreeView_PreviewMouseMove;
            treeView.Drop -= TreeView_Drop;
            treeView.DragOver -= TreeView_DragOver;
        }
    }

    private static void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(null);
    }

    private static void TreeView_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var currentPoint = e.GetPosition(null);
        var diff = _startPoint - currentPoint;

        if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance)
            return;

        var treeView = (TreeView)sender;
        var treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        if (treeViewItem?.DataContext is not TreeEntryViewModel item) return;

        if (item.IsEditing) return;

        _draggedItem = item;
        DragDrop.DoDragDrop(treeView, new DataObject(typeof(TreeEntryViewModel), item), DragDropEffects.Move);
        _draggedItem = null;
    }

    private static void TreeView_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TreeEntryViewModel)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var targetItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        if (targetItem?.DataContext is TreeEntryViewModel target && _draggedItem is not null)
        {
            if (target.Id == _draggedItem.Id || IsDescendant(_draggedItem, target))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private static async void TreeView_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TreeEntryViewModel))) return;

        var draggedItem = (TreeEntryViewModel)e.Data.GetData(typeof(TreeEntryViewModel));
        var targetItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);

        if (targetItem?.DataContext is not TreeEntryViewModel target) return;
        if (draggedItem.Id == target.Id) return;

        var treeView = (TreeView)sender;
        var treeViewModel = GetTreeViewModel(treeView);
        if (treeViewModel is null) return;

        Guid? newParentId;
        int insertIndex;

        if (target.EntryType == Domain.Enums.TreeEntryType.Folder)
        {
            // Drop onto a folder: append as last child
            newParentId = target.Id;
            insertIndex = target.Children.Count;
        }
        else
        {
            // Drop onto a connection: insert after it among its siblings
            newParentId = target.ParentId;
            insertIndex = target.SortOrder + 1;
        }

        await treeViewModel.MoveEntryAsync(draggedItem, newParentId, insertIndex);
    }

    private static bool IsDescendant(TreeEntryViewModel parent, TreeEntryViewModel potentialChild)
    {
        foreach (var child in parent.Children)
        {
            if (child.Id == potentialChild.Id) return true;
            if (IsDescendant(child, potentialChild)) return true;
        }
        return false;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T ancestor) return ancestor;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
