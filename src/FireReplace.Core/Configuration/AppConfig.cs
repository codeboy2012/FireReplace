namespace FireReplace.Core.Configuration;

/// <summary>Theme options. FireReplace is a dark-first application.</summary>
public enum AppTheme
{
    /// <summary>The default dark theme.</summary>
    Dark = 0,

    /// <summary>A slightly deeper, higher-contrast dark variant.</summary>
    Midnight = 1,
}

/// <summary>
/// User preferences persisted to <c>%APPDATA%\FireReplace\settings.json</c>.
/// </summary>
/// <remarks>
/// Only non-sensitive information is stored: the last IP address, paths and UI preferences.
/// No credentials, tokens or ADB keys are ever written by FireReplace.
/// </remarks>
public sealed class AppConfig
{
    /// <summary>Schema version, so future releases can migrate cleanly.</summary>
    public int Version { get; set; } = 1;

    /// <summary>True once the user has completed the first-run safety checklist.</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>Last Fire TV IP address the user connected to.</summary>
    public string? LastIpAddress { get; set; }

    /// <summary>Whether to prefill the last IP address on startup.</summary>
    public bool RememberLastIp { get; set; } = true;

    /// <summary>Whether to attempt a connection automatically at startup using the remembered IP.</summary>
    public bool AutoConnectOnStartup { get; set; }

    /// <summary>ADB port used for network connections.</summary>
    public int AdbPort { get; set; } = 5555;

    /// <summary>Optional override for the platform-tools folder.</summary>
    public string? PlatformToolsDirectory { get; set; }

    /// <summary>Optional override for the APK folder.</summary>
    public string? ApkDirectory { get; set; }

    /// <summary>Connection timeout in seconds.</summary>
    public int ConnectTimeoutSeconds { get; set; } = 8;

    /// <summary>Normal command timeout in seconds.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>Selected theme.</summary>
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    /// <summary>When true, nothing is written to the device; commands are only previewed.</summary>
    public bool DryRun { get; set; }

    /// <summary>When true, every modification requires an explicit confirmation step.</summary>
    public bool ConfirmDestructiveActions { get; set; } = true;

    /// <summary>When true, advanced/experimental groups require a second confirmation.</summary>
    public bool ConfirmAdvancedActions { get; set; } = true;

    /// <summary>When true, a configuration backup is offered before the first modification of a session.</summary>
    public bool OfferBackupBeforeChanges { get; set; } = true;

    /// <summary>Maximum number of log entries kept in memory.</summary>
    public int MaxLogEntries { get; set; } = 2000;

    /// <summary>When true, log entries are appended to a file as they are produced.</summary>
    public bool SaveLogsAutomatically { get; set; }

    /// <summary>Returns a copy, used so the Settings page can edit without committing.</summary>
    public AppConfig Clone() => (AppConfig)MemberwiseClone();
}
