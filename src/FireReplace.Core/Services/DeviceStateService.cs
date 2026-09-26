using FireReplace.Core.Adb;
using FireReplace.Core.Catalog;
using FireReplace.Core.Logging;

namespace FireReplace.Core.Services;

/// <summary>
/// Reads the complete device snapshot used by every page.
/// </summary>
/// <remarks>
/// A full refresh costs three adb invocations, not thirty: the property dump, one batched
/// <c>settings get</c> line, and one batched <c>pm list packages</c> line. The three run
/// concurrently. FireReplace never polls the device on a timer; refreshes happen on connect, after
/// an action, and when the user asks.
/// </remarks>
public sealed class DeviceStateService
{
    private readonly IAdbService _adb;
    private readonly ILogService _log;

    /// <summary>Creates the service.</summary>
    public DeviceStateService(IAdbService adb, ILogService log)
    {
        _adb = adb ?? throw new ArgumentNullException(nameof(adb));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>The most recent snapshot, or null before the first refresh.</summary>
    public DeviceState? Current { get; private set; }

    /// <summary>Raised whenever a new snapshot replaces the current one.</summary>
    public event EventHandler<DeviceState>? StateChanged;

    /// <summary>Drops the cached snapshot, for example after disconnecting.</summary>
    public void Reset()
    {
        Current = null;
    }

    /// <summary>Reads a fresh snapshot from the device.</summary>
    public async Task<DeviceState> RefreshAsync(CancellationToken cancellationToken = default)
    {
        _log.Write(LogLevel.Info, "device", "Reading device information");

        var deviceTask = _adb.GetDeviceInfoAsync(cancellationToken);
        var settingsTask = ReadSettingsAsync(cancellationToken);
        var packagesTask = ReadPackagesAsync(cancellationToken);

        await Task.WhenAll(deviceTask, settingsTask, packagesTask).ConfigureAwait(false);

        var device = await deviceTask.ConfigureAwait(false);
        var settings = await settingsTask.ConfigureAwait(false);
        var (installed, disabled) = await packagesTask.ConfigureAwait(false);

        var accessibilityKey = DeviceState.KeyOf(SettingScope.Secure, "enabled_accessibility_services");
        var listenerKey = DeviceState.KeyOf(SettingScope.Secure, "enabled_notification_listeners");

        var state = new DeviceState
        {
            Device = device,
            Settings = settings,
            AccessibilityServices = AdbOutputParser.ParseComponentList(settings.GetValueOrDefault(accessibilityKey)),
            NotificationListeners = AdbOutputParser.ParseComponentList(settings.GetValueOrDefault(listenerKey)),
            InstalledPackages = installed,
            DisabledPackages = disabled,
            ReadAt = DateTimeOffset.Now,
        };

        Current = state;

        if (device is not null)
        {
            _log.Write(
                LogLevel.Success,
                "device",
                $"{device.Model ?? "device"} detected — Android {device.AndroidRelease ?? "?"}, {installed.Count} package(s), {disabled.Count} disabled");
        }
        else
        {
            _log.Write(LogLevel.Warning, "device", "Device information could not be read.");
        }

        StateChanged?.Invoke(this, state);
        return state;
    }

    /// <summary>
    /// Reads only the package lists. Used after a package action so the UI updates without
    /// re-reading every property.
    /// </summary>
    public async Task<DeviceState> RefreshPackagesAsync(CancellationToken cancellationToken = default)
    {
        var (installed, disabled) = await ReadPackagesAsync(cancellationToken).ConfigureAwait(false);
        var previous = Current ?? new DeviceState();

        var state = new DeviceState
        {
            Device = previous.Device,
            Settings = previous.Settings,
            AccessibilityServices = previous.AccessibilityServices,
            NotificationListeners = previous.NotificationListeners,
            InstalledPackages = installed,
            DisabledPackages = disabled,
            ReadAt = DateTimeOffset.Now,
        };

        Current = state;
        StateChanged?.Invoke(this, state);
        return state;
    }

    /// <summary>
    /// Reads only the settings values. Used after a settings action.
    /// </summary>
    public async Task<DeviceState> RefreshSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ReadSettingsAsync(cancellationToken).ConfigureAwait(false);
        var previous = Current ?? new DeviceState();

        var accessibilityKey = DeviceState.KeyOf(SettingScope.Secure, "enabled_accessibility_services");
        var listenerKey = DeviceState.KeyOf(SettingScope.Secure, "enabled_notification_listeners");

        var state = new DeviceState
        {
            Device = previous.Device,
            Settings = settings,
            AccessibilityServices = AdbOutputParser.ParseComponentList(settings.GetValueOrDefault(accessibilityKey)),
            NotificationListeners = AdbOutputParser.ParseComponentList(settings.GetValueOrDefault(listenerKey)),
            InstalledPackages = previous.InstalledPackages,
            DisabledPackages = previous.DisabledPackages,
            ReadAt = DateTimeOffset.Now,
        };

        Current = state;
        StateChanged?.Invoke(this, state);
        return state;
    }

    /// <summary>The settings definitions read during a refresh, in a stable order.</summary>
    public static IReadOnlyList<SettingDefinition> ReadableSettings { get; } =
        SettingsCatalog.AllReadableSettings.ToArray();

    private async Task<Dictionary<string, string?>> ReadSettingsAsync(CancellationToken cancellationToken)
    {
        var definitions = ReadableSettings;
        var commands = definitions
            .Select(d => (IReadOnlyList<string>)new[] { "settings", "get", d.ScopeToken, d.Key })
            .ToArray();

        var outputs = await _adb.ShellBatchAsync(commands, cancellationToken).ConfigureAwait(false);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);

        for (var i = 0; i < definitions.Count; i++)
        {
            var value = i < outputs.Count ? AdbOutputParser.ParseSettingValue(outputs[i]) : null;
            values[DeviceState.KeyOf(definitions[i])] = value;
        }

        return values;
    }

    private async Task<(IReadOnlySet<string> Installed, IReadOnlySet<string> Disabled)> ReadPackagesAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IReadOnlyList<string>> commands =
        [
            ["pm", "list", "packages"],
            ["pm", "list", "packages", "-d"],
        ];

        var outputs = await _adb.ShellBatchAsync(commands, cancellationToken).ConfigureAwait(false);

        var installed = new HashSet<string>(
            outputs.Count > 0 ? AdbOutputParser.ParsePackageList(outputs[0]) : Array.Empty<string>(),
            StringComparer.Ordinal);

        var disabled = new HashSet<string>(
            outputs.Count > 1 ? AdbOutputParser.ParsePackageList(outputs[1]) : Array.Empty<string>(),
            StringComparer.Ordinal);

        // A disabled package is still installed; some Fire OS builds omit it from the plain list.
        foreach (var package in disabled)
        {
            installed.Add(package);
        }

        return (installed, disabled);
    }
}
