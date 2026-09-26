using FireReplace.Core.Models;

namespace FireReplace.Core.Services;

/// <summary>Contents of <c>device.json</c> inside a backup folder.</summary>
public sealed class BackupDeviceFile
{
    /// <summary>Backup format version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>When the backup was taken.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>FireReplace version that produced the backup.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Device identity at the time of the backup.</summary>
    public DeviceInfo? Device { get; set; }
}

/// <summary>Contents of <c>settings.json</c> inside a backup folder.</summary>
public sealed class BackupSettingsFile
{
    /// <summary>Backup format version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Values keyed by <c>scope/key</c>. A null value records that the key was unset on the device.
    /// </summary>
    public Dictionary<string, string?> Settings { get; set; } = new(StringComparer.Ordinal);

    /// <summary>The accessibility service list exactly as it was.</summary>
    public List<string> AccessibilityServices { get; set; } = [];

    /// <summary>The notification listener list exactly as it was.</summary>
    public List<string> NotificationListeners { get; set; } = [];
}

/// <summary>Contents of <c>packages.json</c> inside a backup folder.</summary>
public sealed class BackupPackagesFile
{
    /// <summary>Backup format version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Enabled/disabled state for the packages FireReplace can change. Private user data is never
    /// included: this is a list of package names and states only.
    /// </summary>
    public Dictionary<string, PackageState> Packages { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>A backup folder on disk.</summary>
/// <param name="Id">Folder name, for example <c>2026-09-25_204102</c>.</param>
/// <param name="Directory">Full path of the folder.</param>
/// <param name="CreatedAt">Timestamp recorded in device.json, falling back to folder time.</param>
/// <param name="DeviceName">Device display name recorded in the backup.</param>
/// <param name="SettingCount">Number of recorded settings.</param>
/// <param name="PackageCount">Number of recorded packages.</param>
public sealed record BackupEntry(
    string Id,
    string Directory,
    DateTimeOffset CreatedAt,
    string DeviceName,
    int SettingCount,
    int PackageCount)
{
    /// <summary>Label shown in the backup list.</summary>
    public string DisplayLabel => $"{CreatedAt:yyyy-MM-dd HH:mm:ss} — {DeviceName}";
}
