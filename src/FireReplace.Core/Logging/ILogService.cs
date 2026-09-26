namespace FireReplace.Core.Logging;

/// <summary>Application log sink. Implementations must be safe to call from any thread.</summary>
public interface ILogService
{
    /// <summary>Raised when an entry is appended.</summary>
    event EventHandler<LogEntry>? EntryAdded;

    /// <summary>Raised when the buffer is cleared.</summary>
    event EventHandler? Cleared;

    /// <summary>A snapshot of the current buffer, oldest first.</summary>
    IReadOnlyList<LogEntry> Snapshot();

    /// <summary>Appends an entry.</summary>
    void Write(LogLevel level, string category, string message, string? detail = null);

    /// <summary>Clears the buffer.</summary>
    void Clear();

    /// <summary>Renders the buffer as plain text.</summary>
    string Render();
}

/// <summary>Convenience wrappers over <see cref="ILogService.Write"/>.</summary>
public static class LogServiceExtensions
{
    /// <summary>Writes an informational entry.</summary>
    public static void Info(this ILogService log, string message, string? detail = null) =>
        log.Write(LogLevel.Info, "app", message, detail);

    /// <summary>Writes a success entry.</summary>
    public static void Success(this ILogService log, string message, string? detail = null) =>
        log.Write(LogLevel.Success, "app", message, detail);

    /// <summary>Writes a warning entry.</summary>
    public static void Warn(this ILogService log, string message, string? detail = null) =>
        log.Write(LogLevel.Warning, "app", message, detail);

    /// <summary>Writes an error entry.</summary>
    public static void Error(this ILogService log, string message, string? detail = null) =>
        log.Write(LogLevel.Error, "app", message, detail);

    /// <summary>Writes a debug entry.</summary>
    public static void Debug(this ILogService log, string message, string? detail = null) =>
        log.Write(LogLevel.Debug, "app", message, detail);
}
