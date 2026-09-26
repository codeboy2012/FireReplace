using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using FireReplace.Core.Logging;
using FireReplace.Mvvm;

namespace FireReplace.ViewModels;

/// <summary>
/// The live command/output panel. Entries arrive from <see cref="LogService"/> and are marshalled
/// onto the UI thread one at a time, so a burst of commands cannot stall the interface.
/// </summary>
public sealed class LogViewModel : ObservableObject
{
    private const int MaxVisibleEntries = 500;

    private readonly LogService _log;
    private bool _showDebug;
    private bool _isExpanded = true;

    /// <summary>Creates the view model and mirrors the existing buffer.</summary>
    public LogViewModel(LogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));

        foreach (var entry in _log.Snapshot())
        {
            Append(entry);
        }

        _log.EntryAdded += OnEntryAdded;
        _log.Cleared += OnCleared;

        CopyCommand = new RelayCommand(Copy);
        ClearCommand = new RelayCommand(() => _log.Clear());
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    /// <summary>Entries currently shown, oldest first.</summary>
    public ObservableCollection<LogEntry> Entries { get; } = [];

    /// <summary>When true, debug entries (individual adb invocations) are shown too.</summary>
    public bool ShowDebug
    {
        get => _showDebug;
        set
        {
            if (SetProperty(ref _showDebug, value))
            {
                Rebuild();
            }
        }
    }

    /// <summary>Whether the panel is expanded.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>Copies the visible log to the clipboard.</summary>
    public RelayCommand CopyCommand { get; }

    /// <summary>Clears the buffer.</summary>
    public RelayCommand ClearCommand { get; }

    /// <summary>Saves the log to a file chosen by the user.</summary>
    public AsyncRelayCommand SaveCommand { get; }

    /// <summary>Collapses or expands the panel.</summary>
    public RelayCommand ToggleExpandCommand { get; }

    /// <summary>Renders the visible entries as text.</summary>
    public string RenderVisible() =>
        string.Join(System.Environment.NewLine, Entries.Select(e => e.ToLine()));

    private void OnEntryAdded(object? sender, LogEntry entry)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Append(entry);
        }
        else
        {
            dispatcher.InvokeAsync(() => Append(entry));
        }
    }

    private void OnCleared(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Entries.Clear();
        }
        else
        {
            dispatcher.InvokeAsync(Entries.Clear);
        }
    }

    private void Append(LogEntry entry)
    {
        if (entry.Level == LogLevel.Debug && !_showDebug)
        {
            return;
        }

        Entries.Add(entry);
        while (Entries.Count > MaxVisibleEntries)
        {
            Entries.RemoveAt(0);
        }
    }

    private void Rebuild()
    {
        Entries.Clear();
        foreach (var entry in _log.Snapshot())
        {
            Append(entry);
        }
    }

    private void Copy()
    {
        try
        {
            Clipboard.SetText(RenderVisible());
            _log.Write(LogLevel.Info, "log", "Log copied to the clipboard.");
        }
        catch (Exception ex)
        {
            _log.Write(LogLevel.Warning, "log", "The clipboard was not available.", ex.Message);
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save FireReplace log",
            FileName = $"firereplace-{DateTime.Now:yyyy-MM-dd_HHmmss}.log",
            Filter = "Log file (*.log)|*.log|Text file (*.txt)|*.txt",
            AddExtension = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await _log.SaveAsync(dialog.FileName, cancellationToken).ConfigureAwait(true);
        _log.Write(LogLevel.Success, "log", $"Log saved to {dialog.FileName}");
    }
}
