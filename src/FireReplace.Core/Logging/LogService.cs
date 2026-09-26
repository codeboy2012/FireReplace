using System.Text;

namespace FireReplace.Core.Logging;

/// <summary>
/// In-memory ring buffer log. Nothing is written to disk unless the user asks for it, either by
/// exporting manually or by enabling automatic saving in Settings.
/// </summary>
public sealed class LogService : ILogService
{
    private readonly object _gate = new();
    private readonly Queue<LogEntry> _entries = new();

    private int _maxEntries;
    private string? _autoSavePath;

    /// <summary>Creates a log service holding at most <paramref name="maxEntries"/> records.</summary>
    public LogService(int maxEntries = 2000)
    {
        _maxEntries = Math.Clamp(maxEntries, 100, 100_000);
    }

    /// <inheritdoc />
    public event EventHandler<LogEntry>? EntryAdded;

    /// <inheritdoc />
    public event EventHandler? Cleared;

    /// <summary>Maximum number of retained entries. Lowering it trims immediately.</summary>
    public int MaxEntries
    {
        get => _maxEntries;
        set
        {
            lock (_gate)
            {
                _maxEntries = Math.Clamp(value, 100, 100_000);
                Trim();
            }
        }
    }

    /// <summary>
    /// When set, every entry is also appended to this file. The caller owns the decision;
    /// FireReplace never enables this on its own.
    /// </summary>
    public void SetAutoSavePath(string? path)
    {
        lock (_gate)
        {
            _autoSavePath = string.IsNullOrWhiteSpace(path) ? null : path;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }

    /// <inheritdoc />
    public void Write(LogLevel level, string category, string message, string? detail = null)
    {
        var entry = new LogEntry(DateTimeOffset.Now, level, category, message, Sanitise(detail));

        string? autoSavePath;
        lock (_gate)
        {
            _entries.Enqueue(entry);
            Trim();
            autoSavePath = _autoSavePath;
        }

        if (autoSavePath is not null)
        {
            TryAppend(autoSavePath, entry);
        }

        EntryAdded?.Invoke(this, entry);
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }

        Cleared?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public string Render()
    {
        var builder = new StringBuilder();
        foreach (var entry in Snapshot())
        {
            builder.AppendLine(entry.ToLine());
        }

        return builder.ToString();
    }

    /// <summary>Writes the current buffer to <paramref name="path"/>.</summary>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, Render(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private void Trim()
    {
        while (_entries.Count > _maxEntries)
        {
            _entries.Dequeue();
        }
    }

    /// <summary>
    /// Strips control characters from device output so a crafted response cannot corrupt the log view.
    /// </summary>
    private static string? Sanitise(string? detail)
    {
        if (string.IsNullOrEmpty(detail))
        {
            return detail;
        }

        var builder = new StringBuilder(detail.Length);
        foreach (var c in detail)
        {
            if (c is '\n' or '\t' || !char.IsControl(c))
            {
                builder.Append(c);
            }
            else if (c == '\r')
            {
                continue;
            }
            else
            {
                builder.Append('\uFFFD');
            }
        }

        return builder.ToString();
    }

    private static void TryAppend(string path, LogEntry entry)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, entry.ToLine() + System.Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Automatic log saving is best effort and must never break an operation.
        }
    }
}
