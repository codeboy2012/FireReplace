using FireReplace.Core.Actions;
using FireReplace.Core.Discovery;
using FireReplace.Core.Logging;

namespace FireReplace.Tests.Fakes;

/// <summary>A log sink that keeps everything in a list so tests can assert on what was recorded.</summary>
public sealed class RecordingLog : ILogService
{
    private readonly List<LogEntry> _entries = [];

    public event EventHandler<LogEntry>? EntryAdded;

    public event EventHandler? Cleared;

    public IReadOnlyList<LogEntry> Entries => _entries;

    public IReadOnlyList<LogEntry> Snapshot() => _entries.ToArray();

    public void Write(LogLevel level, string category, string message, string? detail = null)
    {
        var entry = new LogEntry(DateTimeOffset.Now, level, category, message, detail);
        _entries.Add(entry);
        EntryAdded?.Invoke(this, entry);
    }

    public void Clear()
    {
        _entries.Clear();
        Cleared?.Invoke(this, EventArgs.Empty);
    }

    public string Render() => string.Join(System.Environment.NewLine, _entries.Select(e => e.ToLine()));

    public bool Contains(LogLevel level, string fragment) =>
        _entries.Any(e => e.Level == level && e.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A confirmation host that answers without any UI, and records what it was shown.</summary>
public sealed class ScriptedConfirmationHost : IConfirmationHost
{
    private readonly bool _approve;

    public ScriptedConfirmationHost(bool approve) => _approve = approve;

    public List<ActionPlan> Requests { get; } = [];

    public Task<bool> ConfirmAsync(ActionPlan plan, CancellationToken cancellationToken = default)
    {
        Requests.Add(plan);
        return Task.FromResult(_approve);
    }
}

/// <summary>An in-memory file system for the toolchain locator tests.</summary>
public sealed class FakeFileProbe : IFileProbe
{
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public FakeFileProbe AddFile(string path)
    {
        _files.Add(Path.GetFullPath(path));
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        while (!string.IsNullOrEmpty(directory))
        {
            _directories.Add(directory);
            directory = Path.GetDirectoryName(directory);
        }

        return this;
    }

    public FakeFileProbe AddDirectory(string path)
    {
        _directories.Add(Path.GetFullPath(path));
        return this;
    }

    public bool FileExists(string path) => _files.Contains(Path.GetFullPath(path));

    public bool DirectoryExists(string path) => _directories.Contains(Path.GetFullPath(path));

    public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern)
    {
        var full = Path.GetFullPath(directory);
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(searchPattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";

        return _files
            .Where(f => string.Equals(Path.GetDirectoryName(f), full, StringComparison.OrdinalIgnoreCase))
            .Where(f => System.Text.RegularExpressions.Regex.IsMatch(
                Path.GetFileName(f),
                regex,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
