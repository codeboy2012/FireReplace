using FireReplace.Core.Catalog;
using FireReplace.Core.Validation;

namespace FireReplace.Core.Adb;

/// <summary>
/// Builds ADB argument arrays. This is the only place in FireReplace where an adb command shape is
/// defined, and every dynamic value is validated before it is added to the array.
/// </summary>
/// <remarks>
/// Arguments are always produced as a <see cref="IReadOnlyList{T}"/> of discrete tokens and handed to
/// <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/>. No command line string is ever
/// concatenated, so shell metacharacters in a value cannot become a separate command.
/// </remarks>
public static class AdbArguments
{
    /// <summary>Marker echoed between batched read-only shell commands.</summary>
    public const string BatchSeparator = "__FIREREPLACE__";

    /// <summary>Prefixes device targeting (<c>-s serial</c>) onto a command when a serial is known.</summary>
    public static IReadOnlyList<string> WithTarget(string? serial, params string[] command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(serial))
        {
            return command;
        }

        if (!IsSerial(serial))
        {
            throw new ArgumentException($"'{serial}' is not a usable ADB serial.", nameof(serial));
        }

        var args = new List<string>(command.Length + 2) { "-s", serial };
        args.AddRange(command);
        return args;
    }

    /// <summary>
    /// A serial is either <c>ip:port</c> (network) or a token of safe characters (USB).
    /// </summary>
    public static bool IsSerial(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial) || serial.Length > 128)
        {
            return false;
        }

        var colon = serial.LastIndexOf(':');
        if (colon > 0)
        {
            var host = serial[..colon];
            var portText = serial[(colon + 1)..];
            return Validate.IsIpAddress(host)
                   && int.TryParse(portText, out var port)
                   && Validate.IsPort(port);
        }

        foreach (var c in serial)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Builds <c>connect ip:port</c>.</summary>
    public static IReadOnlyList<string> Connect(string ipAddress, int port)
    {
        var ip = Validate.IpAddress(ipAddress);
        if (!Validate.IsPort(port))
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535.");
        }

        return ["connect", FormatEndpoint(ip, port)];
    }

    /// <summary>Builds <c>disconnect ip:port</c>.</summary>
    public static IReadOnlyList<string> Disconnect(string serial) =>
        IsSerial(serial)
            ? ["disconnect", serial]
            : throw new ArgumentException($"'{serial}' is not a usable ADB serial.", nameof(serial));

    /// <summary>Formats an endpoint, wrapping IPv6 literals in brackets.</summary>
    public static string FormatEndpoint(string ipAddress, int port)
    {
        var ip = Validate.IpAddress(ipAddress);
        return ip.Contains(':', StringComparison.Ordinal) ? $"[{ip}]:{port}" : $"{ip}:{port}";
    }

    /// <summary>Builds <c>devices</c>.</summary>
    public static IReadOnlyList<string> Devices() => ["devices"];

    /// <summary>Builds <c>-s serial shell getprop</c> — a full property dump in one call.</summary>
    public static IReadOnlyList<string> GetPropDump(string? serial) =>
        WithTarget(serial, "shell", "getprop");

    /// <summary>Builds <c>-s serial shell settings get scope key</c>.</summary>
    public static IReadOnlyList<string> GetSetting(string? serial, SettingScope scope, string key) =>
        WithTarget(serial, "shell", "settings", "get", ScopeToken(scope), Validate.SettingKey(key));

    /// <summary>Builds <c>-s serial shell settings put scope key value</c>.</summary>
    public static IReadOnlyList<string> PutSetting(string? serial, SettingScope scope, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        // Settings values may legitimately contain ':' and '/' (component lists) but never whitespace
        // or shell metacharacters, and ProcessStartInfo.ArgumentList keeps them as one token regardless.
        if (value.Length > 4096 || value.Any(char.IsControl))
        {
            throw new ArgumentException("Settings value contains control characters or is too long.", nameof(value));
        }

        return WithTarget(serial, "shell", "settings", "put", ScopeToken(scope), Validate.SettingKey(key), value);
    }

    /// <summary>Builds <c>-s serial shell settings delete scope key</c>.</summary>
    public static IReadOnlyList<string> DeleteSetting(string? serial, SettingScope scope, string key) =>
        WithTarget(serial, "shell", "settings", "delete", ScopeToken(scope), Validate.SettingKey(key));

    /// <summary>Builds <c>-s serial shell pm list packages [filter]</c>.</summary>
    public static IReadOnlyList<string> ListPackages(string? serial, PackageListFilter filter) => filter switch
    {
        PackageListFilter.All => WithTarget(serial, "shell", "pm", "list", "packages"),
        PackageListFilter.Enabled => WithTarget(serial, "shell", "pm", "list", "packages", "-e"),
        PackageListFilter.Disabled => WithTarget(serial, "shell", "pm", "list", "packages", "-d"),
        PackageListFilter.System => WithTarget(serial, "shell", "pm", "list", "packages", "-s"),
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown package filter."),
    };

    /// <summary>Builds <c>-s serial shell pm disable-user --user 0 package</c>.</summary>
    public static IReadOnlyList<string> DisablePackage(string? serial, string packageName) =>
        WithTarget(serial, "shell", "pm", "disable-user", "--user", "0", Validate.PackageName(packageName));

    /// <summary>Builds <c>-s serial shell pm enable package</c>.</summary>
    public static IReadOnlyList<string> EnablePackage(string? serial, string packageName) =>
        WithTarget(serial, "shell", "pm", "enable", Validate.PackageName(packageName));

    /// <summary>Builds <c>-s serial shell pm grant package permission</c>.</summary>
    public static IReadOnlyList<string> GrantPermission(string? serial, string packageName, string permission)
    {
        if (!IsPermissionName(permission))
        {
            throw new ArgumentException($"'{permission}' is not a valid permission name.", nameof(permission));
        }

        return WithTarget(serial, "shell", "pm", "grant", Validate.PackageName(packageName), permission);
    }

    /// <summary>Builds <c>-s serial install -r -g path</c>.</summary>
    public static IReadOnlyList<string> Install(string? serial, string apkPath, bool grantRuntimePermissions)
    {
        if (!Validate.IsExistingApkPath(apkPath))
        {
            throw new ArgumentException($"'{apkPath}' is not an existing .apk file.", nameof(apkPath));
        }

        var full = Path.GetFullPath(apkPath);
        return grantRuntimePermissions
            ? WithTarget(serial, "install", "-r", "-g", full)
            : WithTarget(serial, "install", "-r", full);
    }

    /// <summary>Builds <c>-s serial shell cmd notification allow_listener component</c>.</summary>
    public static IReadOnlyList<string> AllowNotificationListener(string? serial, string component) =>
        WithTarget(serial, "shell", "cmd", "notification", "allow_listener", Validate.ComponentName(component));

    /// <summary>Builds <c>-s serial shell cmd notification disallow_listener component</c>.</summary>
    public static IReadOnlyList<string> DisallowNotificationListener(string? serial, string component) =>
        WithTarget(serial, "shell", "cmd", "notification", "disallow_listener", Validate.ComponentName(component));

    /// <summary>
    /// Builds <c>-s serial shell am start -a ACTION</c> for one of a fixed set of system settings
    /// screens. Arbitrary intents are deliberately not supported.
    /// </summary>
    public static IReadOnlyList<string> StartSettingsActivity(string? serial, string action)
    {
        if (!AllowedIntentActions.Contains(action))
        {
            throw new ArgumentException($"Intent action '{action}' is not on the allow list.", nameof(action));
        }

        return WithTarget(serial, "shell", "am", "start", "-a", action);
    }

    /// <summary>Intent actions FireReplace is permitted to launch.</summary>
    public static readonly IReadOnlySet<string> AllowedIntentActions = new HashSet<string>(StringComparer.Ordinal)
    {
        "android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS",
        "android.settings.ACCESSIBILITY_SETTINGS",
        "android.settings.SETTINGS",
    };

    /// <summary>Builds <c>-s serial reboot</c>.</summary>
    public static IReadOnlyList<string> Reboot(string? serial) => WithTarget(serial, "reboot");

    /// <summary>Builds <c>start-server</c>.</summary>
    public static IReadOnlyList<string> StartServer() => ["start-server"];

    /// <summary>Builds <c>kill-server</c>.</summary>
    public static IReadOnlyList<string> KillServer() => ["kill-server"];

    /// <summary>Builds <c>-s serial get-state</c>.</summary>
    public static IReadOnlyList<string> GetState(string? serial) => WithTarget(serial, "get-state");

    /// <summary>Builds <c>version</c>.</summary>
    public static IReadOnlyList<string> Version() => ["version"];

    /// <summary>
    /// Builds a single <c>shell</c> invocation that runs several read-only commands, separated by an
    /// echoed marker. Used to collect status in one process instead of a dozen.
    /// </summary>
    /// <param name="serial">Target serial.</param>
    /// <param name="commands">
    /// Each command as a token list. Every token must satisfy
    /// <see cref="Validate.IsShellSafeToken"/>, which excludes whitespace and all shell metacharacters.
    /// </param>
    public static IReadOnlyList<string> ShellBatch(string? serial, IReadOnlyList<IReadOnlyList<string>> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (commands.Count == 0)
        {
            throw new ArgumentException("At least one command is required.", nameof(commands));
        }

        var parts = new List<string>(commands.Count);
        foreach (var command in commands)
        {
            if (command.Count == 0)
            {
                throw new ArgumentException("A batched command cannot be empty.", nameof(commands));
            }

            foreach (var token in command)
            {
                if (!Validate.IsShellSafeToken(token))
                {
                    throw new ArgumentException(
                        $"Token '{token}' is not safe to batch. Run this command on its own instead.",
                        nameof(commands));
                }
            }

            parts.Add(string.Join(' ', command));
        }

        var line = string.Join($"; echo {BatchSeparator}; ", parts);
        return WithTarget(serial, "shell", line);
    }

    /// <summary>Maps a scope to its command-line token.</summary>
    public static string ScopeToken(SettingScope scope) => scope switch
    {
        SettingScope.Global => "global",
        SettingScope.Secure => "secure",
        SettingScope.System => "system",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown settings scope."),
    };

    /// <summary>True for an <c>android.permission.X</c>-shaped permission name.</summary>
    public static bool IsPermissionName(string? permission) =>
        Validate.IsPackageName(permission) && permission.Contains('.', StringComparison.Ordinal);
}

/// <summary>Filters supported by <c>pm list packages</c>.</summary>
public enum PackageListFilter
{
    /// <summary>All installed packages.</summary>
    All,

    /// <summary>Only enabled packages (<c>-e</c>).</summary>
    Enabled,

    /// <summary>Only disabled packages (<c>-d</c>).</summary>
    Disabled,

    /// <summary>Only system packages (<c>-s</c>).</summary>
    System,
}
