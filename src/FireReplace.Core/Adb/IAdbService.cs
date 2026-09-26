using FireReplace.Core.Catalog;
using FireReplace.Core.Models;

namespace FireReplace.Core.Adb;

/// <summary>
/// The single gateway to adb.exe. Every ADB interaction in FireReplace goes through this service;
/// no view, view model or other service builds a process command itself.
/// </summary>
public interface IAdbService
{
    /// <summary>Full path to the adb executable in use, or null when it has not been located.</summary>
    string? AdbPath { get; set; }

    /// <summary>Serial of the device subsequent commands target, or null for "the only device".</summary>
    string? Serial { get; set; }

    /// <summary>When true, mutating commands are logged and skipped instead of executed.</summary>
    bool DryRun { get; set; }

    /// <summary>Timeout applied to connection attempts.</summary>
    TimeSpan ConnectTimeout { get; set; }

    /// <summary>Timeout applied to normal commands.</summary>
    TimeSpan CommandTimeout { get; set; }

    /// <summary>Raised after every invocation, including dry runs, so the UI can mirror it into the log.</summary>
    event EventHandler<AdbCommandResult>? CommandCompleted;

    /// <summary>Reports the adb client version, used to verify the toolchain works at all.</summary>
    Task<AdbCommandResult> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>Connects to <paramref name="ipAddress"/> on <paramref name="port"/>.</summary>
    Task<AdbCommandResult> ConnectAsync(string ipAddress, int port = 5555, CancellationToken cancellationToken = default);

    /// <summary>Disconnects the given serial, or the current one when omitted.</summary>
    Task<AdbCommandResult> DisconnectAsync(string? serial = null, CancellationToken cancellationToken = default);

    /// <summary>Lists devices known to the local adb server.</summary>
    Task<IReadOnlyList<AdbDeviceEntry>> GetDevicesAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the full property dump from the device in a single call.</summary>
    Task<DeviceInfo?> GetDeviceInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs one read-only shell command built from validated tokens.
    /// </summary>
    Task<AdbCommandResult> ShellAsync(IReadOnlyList<string> tokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs several read-only shell commands in one adb invocation and returns their outputs in order.
    /// </summary>
    Task<IReadOnlyList<string>> ShellBatchAsync(
        IReadOnlyList<IReadOnlyList<string>> commands,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a single setting value, or null when unset.</summary>
    Task<string?> GetSettingAsync(SettingScope scope, string key, CancellationToken cancellationToken = default);

    /// <summary>Writes a single setting value.</summary>
    Task<AdbCommandResult> PutSettingAsync(SettingScope scope, string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Lists packages using the given filter.</summary>
    Task<IReadOnlyList<string>> ListPackagesAsync(PackageListFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Disables a package for user 0 with <c>pm disable-user</c>.</summary>
    Task<AdbCommandResult> DisablePackageAsync(string packageName, CancellationToken cancellationToken = default);

    /// <summary>Re-enables a package with <c>pm enable</c>.</summary>
    Task<AdbCommandResult> EnablePackageAsync(string packageName, CancellationToken cancellationToken = default);

    /// <summary>Grants a permission with <c>pm grant</c>.</summary>
    Task<AdbCommandResult> GrantPermissionAsync(string packageName, string permission, CancellationToken cancellationToken = default);

    /// <summary>Installs (or reinstalls) an APK from a local path.</summary>
    Task<AdbCommandResult> InstallApkAsync(string apkPath, bool grantRuntimePermissions = false, CancellationToken cancellationToken = default);

    /// <summary>Allows a notification listener component via <c>cmd notification allow_listener</c>.</summary>
    Task<AdbCommandResult> AllowNotificationListenerAsync(string component, CancellationToken cancellationToken = default);

    /// <summary>Opens one of the allow-listed system settings screens on the device.</summary>
    Task<AdbCommandResult> OpenSettingsScreenAsync(string action, CancellationToken cancellationToken = default);

    /// <summary>Reboots the device.</summary>
    Task<AdbCommandResult> RebootAsync(CancellationToken cancellationToken = default);
}
