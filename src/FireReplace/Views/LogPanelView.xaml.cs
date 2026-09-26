using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FireReplace.ViewModels;

namespace FireReplace.Views;

/// <summary>
/// The live log panel. It auto-scrolls to the newest entry, but only while the list is already near
/// the bottom, so reading back through the log is not interrupted by new output.
/// </summary>
public partial class LogPanelView : UserControl
{
    private INotifyCollectionChanged? _observed;

    /// <summary>Creates the view.</summary>
    public LogPanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnEntriesChanged;
            _observed = null;
        }

        if (e.NewValue is LogViewModel viewModel)
        {
            _observed = viewModel.Entries;
            _observed.CollectionChanged += OnEntriesChanged;
        }
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || EntryList.Items.Count == 0)
        {
            return;
        }

        var scrollViewer = FindScrollViewer(EntryList);
        if (scrollViewer is not null && scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight - 24)
        {
            return;
        }

        EntryList.ScrollIntoView(EntryList.Items[^1]);
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer viewer)
        {
            return viewer;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
