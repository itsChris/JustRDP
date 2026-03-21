using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using JustRDP.Presentation.ViewModels;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using DataObject = System.Windows.DataObject;
using DragDrop = System.Windows.DragDrop;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using TreeView = System.Windows.Controls.TreeView;
using TreeViewItem = System.Windows.Controls.TreeViewItem;

namespace JustRDP.Presentation.Behaviors;

public enum DropPosition { Before, Into, After }

public static class TreeViewDragDropBehavior
{
    private const double HeaderHeight = 26.0;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(TreeViewDragDropBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty TreeViewModelProperty =
        DependencyProperty.RegisterAttached("TreeViewModel", typeof(TreeViewModel), typeof(TreeViewDragDropBehavior));

    private static Point _startPoint;
    private static TreeEntryViewModel? _draggedItem;
    private static DropAdorner? _currentAdorner;
    private static TreeViewItem? _lastAdornedItem;

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
            treeView.DragLeave += TreeView_DragLeave;
            treeView.AllowDrop = true;
        }
        else
        {
            treeView.PreviewMouseLeftButtonDown -= TreeView_PreviewMouseLeftButtonDown;
            treeView.PreviewMouseMove -= TreeView_PreviewMouseMove;
            treeView.Drop -= TreeView_Drop;
            treeView.DragOver -= TreeView_DragOver;
            treeView.DragLeave -= TreeView_DragLeave;
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

        if (item.IsEditing || item.IsDashboard) return;

        _draggedItem = item;
        DragDrop.DoDragDrop(treeView, new DataObject(typeof(TreeEntryViewModel), item), DragDropEffects.Move);
        _draggedItem = null;
        RemoveAdorner();
    }

    private static void TreeView_DragLeave(object sender, DragEventArgs e)
    {
        var treeView = (TreeView)sender;
        var pos = e.GetPosition(treeView);
        if (pos.X < 0 || pos.Y < 0 ||
            pos.X > treeView.ActualWidth || pos.Y > treeView.ActualHeight)
        {
            RemoveAdorner();
        }
    }

    private static void TreeView_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TreeEntryViewModel)) || _draggedItem is null)
        {
            RemoveAdorner();
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var targetItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);

        // Dropping on empty space → root level
        if (targetItem?.DataContext is not TreeEntryViewModel target)
        {
            RemoveAdorner();
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (target.IsDashboard || target.Id == _draggedItem.Id || IsDescendant(_draggedItem, target))
        {
            RemoveAdorner();
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var position = GetDropPosition(e, targetItem, target);
        ShowAdorner(targetItem, position);

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private static DropPosition GetDropPosition(DragEventArgs e, TreeViewItem item, TreeEntryViewModel target)
    {
        var pos = e.GetPosition(item);
        var zone = HeaderHeight / 3.0;

        if (pos.Y < zone)
            return DropPosition.Before;
        if (pos.Y > zone * 2)
            return DropPosition.After;

        // Middle zone: "Into" only for folders
        return target.IsFolder && !target.IsDashboard ? DropPosition.Into : DropPosition.After;
    }

    private static async void TreeView_Drop(object sender, DragEventArgs e)
    {
        RemoveAdorner();

        if (!e.Data.GetDataPresent(typeof(TreeEntryViewModel))) return;

        var draggedItem = (TreeEntryViewModel)e.Data.GetData(typeof(TreeEntryViewModel));
        var treeView = (TreeView)sender;
        var treeViewModel = GetTreeViewModel(treeView);
        if (treeViewModel is null) return;

        var targetItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);

        // Drop on empty space → root level (append at end)
        if (targetItem?.DataContext is not TreeEntryViewModel target)
        {
            await treeViewModel.MoveEntryToPositionAsync(draggedItem, null, DropPosition.After);
            return;
        }

        if (target.IsDashboard || draggedItem.Id == target.Id || IsDescendant(draggedItem, target)) return;

        var position = GetDropPosition(e, targetItem, target);
        await treeViewModel.MoveEntryToPositionAsync(draggedItem, target, position);
    }

    private static void ShowAdorner(TreeViewItem item, DropPosition position)
    {
        if (_lastAdornedItem == item && _currentAdorner?.Position == position)
            return;

        RemoveAdorner();

        var layer = AdornerLayer.GetAdornerLayer(item);
        if (layer is null) return;

        _currentAdorner = new DropAdorner(item, position);
        _lastAdornedItem = item;
        layer.Add(_currentAdorner);
    }

    private static void RemoveAdorner()
    {
        if (_currentAdorner is not null && _lastAdornedItem is not null)
        {
            var layer = AdornerLayer.GetAdornerLayer(_lastAdornedItem);
            layer?.Remove(_currentAdorner);
        }
        _currentAdorner = null;
        _lastAdornedItem = null;
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

internal class DropAdorner : Adorner
{
    public DropPosition Position { get; }

    private static readonly Pen LinePen = CreatePen();
    private static readonly Brush HighlightBrush = new SolidColorBrush(Color.FromArgb(50, 33, 150, 243));
    private static readonly Pen HighlightPen = new(new SolidColorBrush(Color.FromRgb(33, 150, 243)), 1);

    private static Pen CreatePen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(33, 150, 243)), 2);
        pen.Freeze();
        return pen;
    }

    static DropAdorner()
    {
        HighlightBrush.Freeze();
        HighlightPen.Freeze();
    }

    public DropAdorner(UIElement adornedElement, DropPosition position) : base(adornedElement)
    {
        Position = position;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = AdornedElement.RenderSize.Width;
        const double headerHeight = 26.0;

        switch (Position)
        {
            case DropPosition.Before:
                dc.DrawLine(LinePen, new Point(0, 1), new Point(width, 1));
                dc.DrawEllipse(LinePen.Brush, null, new Point(4, 1), 3, 3);
                break;
            case DropPosition.After:
                dc.DrawLine(LinePen, new Point(0, headerHeight - 1), new Point(width, headerHeight - 1));
                dc.DrawEllipse(LinePen.Brush, null, new Point(4, headerHeight - 1), 3, 3);
                break;
            case DropPosition.Into:
                dc.DrawRoundedRectangle(HighlightBrush, HighlightPen,
                    new Rect(0, 0, width, headerHeight), 3, 3);
                break;
        }
    }
}
