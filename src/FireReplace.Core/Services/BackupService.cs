using System.Text.Json;
using System.Text.Json.Serialization;
using FireReplace.Core.Actions;
using FireReplace.Core.Adb;
using FireReplace.Core.Catalog;
using FireReplace.Core.Logging;
using FireReplace.Core.Models;

namespace FireReplace.Core.Services;

/// <summary>
/// Saves and restores the configuration FireReplace itself can change.
/// </summary>
/// <remarks>
/// A backup contains three small JSON files: device identity, the settings FireReplace manages, and
/// the enabled/disabled state of the packages in its catalog. It deliberately does not attempt to
/// back up apps, accounts or any private user data — that is not something ADB should be used for here.
/// </remarks>
public sealed class BackupService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _rootDirectory;
    private readonly ILogService _log;
    private readonly IAdbService _adb;

    /// <summary>Creates the service.</summary>
    /// <param name="rootDirectory">Folder that holds one sub-folder per backup.</param>
    /// <param name="adb">Used to build restore operations.</param>
    /// <param name="log">Log sink.</param>
    public BackupService(string rootDirectory, IAdbService adb, ILogService log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = rootDirectory;
        _adb = adb ?? throw new ArgumentNullException(nameof(adb));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Root folder of the backup store.</summary>
    public string RootDirectory => _rootDirectory;

    /// <summary>File names written into each backup folder.</summary>
    public static IReadOnlyList<string> FileNames { get; } = ["device.json", "settings.json", "packages.json"];

    /// <summary>Writes a new backup folder from the given snapshot and returns its entry.</summary>
    public async Task<BackupEntry> CreateAsync(DeviceState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var id = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var directory = Path.Combine(_rootDirectory, id);
        Directory.CreateDirectory(directory);

        var deviceFile = new BackupDeviceFile
        {
            CreatedAt = DateTimeOffset.Now,
            CreatedBy = $"FireReplace {typeof(BackupService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}",
            Device = state.Device,
        };

        var settingsFile = new BackupSettingsFile
        {
            Settings = new Dictionary<string, string?>(state.Settings, StringComparer.Ordinal),
            AccessibilityServices = [.. state.AccessibilityServices],
            NotificationListeners = [.. state.NotificationListeners],
        };

        var packagesFile = new BackupPackagesFile();
        foreach (var entry in PackageCatalog.All)
        {
            packagesFile.Packages[entry.Name] = state.StateOf(entry.Name);
        }

        await WriteAsync(Path.Combine(directory, "device.json"), deviceFile, cancellationToken).ConfigureAwait(false);
        await WriteAsync(Path.Combine(directory, "settings.json"), settingsFile, cancellationToken).ConfigureAwait(false);
        await WriteAsync(Path.Combine(directory, "packages.json"), packagesFile, cancellationToken).ConfigureAwait(false);

        _log.Write(LogLevel.Success, "backup", $"Backup saved to backups\\{id}");

        return new BackupEntry(
            id,
            directory,
            deviceFile.CreatedAt,
            state.Device?.DisplayName ?? "Unknown device",
            settingsFile.Settings.Count,
            packagesFile.Packages.Count);
    }

    /// <summary>Lists available backups, newest first. Unreadable folders are skipped.</summary>
    public IReadOnlyList<BackupEntry> List()
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return Array.Empty<BackupEntry>();
        }

        var entries = new List<BackupEntry>();

        foreach (var directory in Directory.GetDirectories(_rootDirectory))
        {
            var deviceFile = TryRead<BackupDeviceFile>(Path.Combine(directory, "device.json"));
            var settingsFile = TryRead<BackupSettingsFile>(Path.Combine(directory, "settings.json"));
            var packagesFile = TryRead<BackupPackagesFile>(Path.Combine(directory, "packages.json"));

            if (deviceFile is null && settingsFile is null && packagesFile is null)
            {
                continue;
            }

            entries.Add(new BackupEntry(
                Path.GetFileName(directory),
                directory,
                deviceFile?.CreatedAt ?? new DateTimeOffset(Directory.GetCreationTime(directory)),
                deviceFile?.Device?.DisplayName ?? "Unknown device",
                settingsFile?.Settings.Count ?? 0,
                packagesFile?.Packages.Count ?? 0));
        }

        return entries.OrderByDescending(e => e.CreatedAt).ToArray();
    }

    /// <summary>Reads the three files of a backup folder.</summary>
    public (BackupDeviceFile? Device, BackupSettingsFile? Settings, BackupPackagesFile? Packages) Read(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return (
            TryRead<BackupDeviceFile>(Path.Combine(directory, "device.json")),
            TryRead<BackupSettingsFile>(Path.Combine(directory, "settings.json")),
            TryRead<BackupPackagesFile>(Path.Combine(directory, "packages.json")));
    }

    /// <summary>
    /// Builds a plan that returns the device to the state recorded in a backup. Only differences are
    /// included, and the plan is shown for review like every other action.
    /// </summary>
    public ActionPlan BuildRestorePlan(BackupEntry entry, DeviceState current)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(current);

        var (_, settingsFile, packagesFile) = Read(entry.Directory);
        var changes = new List<PlannedChange>();
        var definitionsByKey = DeviceStateService.ReadableSettings.ToDictionary(DeviceState.KeyOf, d => d);

        if (settingsFile is not null)
        {
            foreach (var (key, backedUpValue) in settingsFile.Settings)
            {
                if (!definitionsByKey.TryGetValue(key, out var definition))
                {
                    continue;
                }

                // A key that was unset at backup time is left alone rather than guessed at.
                if (backedUpValue is null)
                {
                    continue;
                }

                var currentValue = current.Settings.GetValueOrDefault(key);
                if (string.Equals(currentValue, backedUpValue, StringComparison.Ordinal))
                {
                    continue;
                }

                changes.Add(new PlannedChange(
                    ChangeKind.SettingWrite,
                    definition.DisplayName,
                    $"{definition.ScopeToken} {definition.Key}",
                    currentValue ?? "(not set)",
                    backedUpValue,
                    ct => _adb.PutSettingAsync(definition.Scope, definition.Key, backedUpValue, ct),
                    AdbCommandResult.Format(AdbArguments.PutSetting(_adb.Serial, definition.Scope, definition.Key, backedUpValue)),
                    "Restored from backup."));
            }
        }

        if (packagesFile is not null)
        {
            foreach (var (packageName, backedUpState) in packagesFile.Packages)
            {
                var currentState = current.StateOf(packageName);
                if (currentState == backedUpState || currentState == PackageState.NotInstalled)
                {
                    continue;
                }

                if (backedUpState == PackageState.Enabled && currentState == PackageState.Disabled)
                {
                    changes.Add(new PlannedChange(
                        ChangeKind.PackageEnable,
                        "Enable package",
                        packageName,
                        "Disabled",
                        "Enabled",
                        ct => _adb.EnablePackageAsync(packageName, ct),
                        AdbCommandResult.Format(AdbArguments.EnablePackage(_adb.Serial, packageName)),
                        "Restored from backup."));
                }
                else if (backedUpState == PackageState.Disabled
                         && currentState == PackageState.Enabled
                         && !PackageCatalog.IsGuarded(packageName))
                {
                    changes.Add(new PlannedChange(
                        ChangeKind.PackageDisable,
                        "Disable package",
                        packageName,
                        "Enabled",
                        "Disabled",
                        ct => _adb.DisablePackageAsync(packageName, ct),
                        AdbCommandResult.Format(AdbArguments.DisablePackage(_adb.Serial, packageName)),
                        "Restored from backup."));
                }
            }
        }

        return new ActionPlan(
            $"Restore backup {entry.Id}",
            "Every difference between the backup and the device is listed below. Accessibility and notification "
            + "lists are shown for reference but are not rewritten automatically, because they may have changed "
            + "for reasons unrelated to FireReplace.",
            changes,
            ConfirmLabel: "Restore",
            IsDestructive: true);
    }

    private static async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken).ConfigureAwait(false);
    }

    private T? TryRead<T>(string path)
        where T : class
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            _log.Write(LogLevel.Warning, "backup", $"Could not read {Path.GetFileName(path)}", e.Message);
            return null;
        }
    }
}
