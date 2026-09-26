namespace FireReplace.Core.Logging;

/// <summary>Severity of a log entry.</summary>
public enum LogLevel
{
    /// <summary>Low-level detail, hidden unless verbose logging is on.</summary>
    Debug,

    /// <summary>Normal progress information.</summary>
    Info,

    /// <summary>An operation completed successfully.</summary>
    Success,

    /// <summary>Something unexpected that did not stop the operation.</summary>
    Warning,

    /// <summary>An operation failed.</summary>
    Error,

    /// <summary>A command that was not executed because dry run is on.</summary>
    DryRun,
}

/// <summary>A single structured log record.</summary>
/// <param name="Timestamp">When the entry was created.</param>
/// <param name="Level">Severity.</param>
/// <param name="Category">Subsystem, for example <c>adb</c> or <c>packages</c>.</param>
/// <param name="Message">Human readable message.</param>
/// <param name="Detail">Optional multi-line detail such as captured stderr.</param>
public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Category,
    string Message,
    string? Detail = null)
{
    /// <summary>Glyph shown in the live log view.</summary>
    public string Glyph => Level switch
    {
        LogLevel.Success => "\u2713",
        LogLevel.Error => "\u2717",
        LogLevel.Warning => "!",
        LogLevel.DryRun => "\u25CB",
        LogLevel.Debug => "\u00B7",
        _ => "\u2022",
    };

    /// <summary>Single-line representation used for clipboard copy and log export.</summary>
    public string ToLine() =>
        Detail is null
            ? $"[{Timestamp:HH:mm:ss}] {Glyph} {Message}"
            : $"[{Timestamp:HH:mm:ss}] {Glyph} {Message}{System.Environment.NewLine}           {Detail.Replace("\n", "\n           ", StringComparison.Ordinal)}";
}
