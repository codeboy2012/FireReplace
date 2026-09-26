using FireReplace.Core.Models;

namespace FireReplace.Core.Adb;

/// <summary>
/// Pure parsers for ADB output. All device output is treated as untrusted text: it is only ever
/// parsed and displayed, never executed, and never fed back into a command without validation.
/// </summary>
public static class AdbOutputParser
{
    private static readonly char[] LineSeparators = ['\r', '\n'];

    /// <summary>Splits output into trimmed, non-empty lines.</summary>
    public static IReadOnlyList<string> Lines(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<string>();
        }

        var lines = output.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                result.Add(trimmed);
            }
        }

        return result;
    }

    /// <summary>Parses <c>adb devices</c> output into entries, skipping the header and daemon chatter.</summary>
    public static IReadOnlyList<AdbDeviceEntry> ParseDevices(string? output)
    {
        var entries = new List<AdbDeviceEntry>();

        foreach (var line in Lines(output))
        {
            if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("* daemon", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("adb server", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var serial = parts[0];
            var rawState = string.Join(' ', parts.Skip(1));
            entries.Add(new AdbDeviceEntry(serial, ParseDeviceState(rawState), rawState));
        }

        return entries;
    }

    /// <summary>Maps an <c>adb devices</c> state token to <see cref="AdbDeviceState"/>.</summary>
    public static AdbDeviceState ParseDeviceState(string? rawState)
    {
        if (string.IsNullOrWhiteSpace(rawState))
        {
            return AdbDeviceState.Unknown;
        }

        var value = rawState.Trim().ToLowerInvariant();

        if (value.StartsWith("device", StringComparison.Ordinal))
        {
            return AdbDeviceState.Online;
        }

        if (value.StartsWith("unauthorized", StringComparison.Ordinal))
        {
            return AdbDeviceState.Unauthorized;
        }

        if (value.StartsWith("offline", StringComparison.Ordinal))
        {
            return AdbDeviceState.Offline;
        }

        if (value.StartsWith("authorizing", StringComparison.Ordinal))
        {
            return AdbDeviceState.Authorizing;
        }

        if (value.StartsWith("connecting", StringComparison.Ordinal)
            || value.StartsWith("no permissions", StringComparison.Ordinal)
            || value.StartsWith("host", StringComparison.Ordinal))
        {
            return AdbDeviceState.Connecting;
        }

        return AdbDeviceState.Unknown;
    }

    /// <summary>
    /// Parses a full <c>getprop</c> dump in <c>[key]: [value]</c> form. One process call gives every
    /// property, which is much faster than a dozen individual <c>getprop key</c> invocations.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseGetPropDump(string? output)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in Lines(output))
        {
            if (line.Length < 6 || line[0] != '[')
            {
                continue;
            }

            var keyEnd = line.IndexOf(']');
            if (keyEnd <= 1)
            {
                continue;
            }

            var key = line[1..keyEnd];

            var valueStart = line.IndexOf('[', keyEnd);
            var valueEnd = line.LastIndexOf(']');
            if (valueStart < 0 || valueEnd <= valueStart)
            {
                continue;
            }

            var value = line[(valueStart + 1)..valueEnd];
            properties[key] = value;
        }

        return properties;
    }

    /// <summary>Builds a <see cref="DeviceInfo"/> from a parsed property dump.</summary>
    public static DeviceInfo BuildDeviceInfo(string serial, IReadOnlyDictionary<string, string> properties)
    {
        string? Get(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        var manufacturer = Get("ro.product.manufacturer", "ro.product.brand");
        var model = Get("ro.product.model");
        var marketing = Get("ro.product.vendor.model", "ro.product.device.name");

        return new DeviceInfo
        {
            Serial = serial,
            Model = model,
            Product = Get("ro.build.product", "ro.product.device"),
            Manufacturer = manufacturer,
            ProductName = Get("ro.product.name"),
            AndroidRelease = Get("ro.build.version.release"),
            SdkLevel = Get("ro.build.version.sdk"),
            BuildIncremental = Get("ro.build.version.incremental"),
            FireOsVersion = Get("ro.build.version.name", "ro.build.version.fireos"),
            Abi = Get("ro.product.cpu.abi"),
            FriendlyName = BuildFriendlyName(manufacturer, marketing, model),
            ReadAt = DateTimeOffset.Now,
        };
    }

    private static string? BuildFriendlyName(string? manufacturer, string? marketing, string? model)
    {
        var name = marketing ?? model;
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(manufacturer))
        {
            return name;
        }

        var pretty = Capitalise(manufacturer);
        return name.StartsWith(pretty, StringComparison.OrdinalIgnoreCase) ? name : $"{pretty} {name}";
    }

    private static string Capitalise(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    /// <summary>
    /// Parses <c>pm list packages</c> output. Lines look like <c>package:com.example</c>; some
    /// builds append the APK path, which is discarded.
    /// </summary>
    public static IReadOnlyList<string> ParsePackageList(string? output)
    {
        var packages = new List<string>();

        foreach (var line in Lines(output))
        {
            if (!line.StartsWith("package:", StringComparison.Ordinal))
            {
                continue;
            }

            var value = line["package:".Length..].Trim();

            // "package:/data/app/base.apk=com.example" form.
            var equals = value.LastIndexOf('=');
            if (equals >= 0)
            {
                value = value[(equals + 1)..].Trim();
            }

            if (value.Length > 0)
            {
                packages.Add(value);
            }
        }

        return packages;
    }

    /// <summary>
    /// Normalises a <c>settings get</c> result. Android prints the literal text <c>null</c> when a
    /// key is unset, which becomes <see langword="null"/> here.
    /// </summary>
    public static string? ParseSettingValue(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var value = output.Trim();
        return string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    /// <summary>Splits a colon-separated component list (accessibility services, notification listeners).</summary>
    public static IReadOnlyList<string> ParseComponentList(string? value)
    {
        var normalised = ParseSettingValue(value);
        if (normalised is null)
        {
            return Array.Empty<string>();
        }

        return normalised
            .Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// True when the output indicates Fire OS refused the operation with a security exception.
    /// </summary>
    public static bool IndicatesSecurityException(string? output) =>
        output is not null
        && (output.Contains("SecurityException", StringComparison.OrdinalIgnoreCase)
            || output.Contains("Permission Denial", StringComparison.OrdinalIgnoreCase)
            || output.Contains("not allowed to disable", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True when <c>pm install</c> output reports success. <c>pm</c> frequently exits 0 while
    /// printing a failure, so the text must be inspected too.
    /// </summary>
    public static bool IndicatesInstallSuccess(string? output) =>
        output is not null && output.Contains("Success", StringComparison.OrdinalIgnoreCase);

    /// <summary>Extracts the <c>Failure [REASON]</c> code from <c>pm install</c> output, when present.</summary>
    public static string? ExtractInstallFailure(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var index = output.IndexOf("Failure", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var start = output.IndexOf('[', index);
        var end = start >= 0 ? output.IndexOf(']', start) : -1;
        return start >= 0 && end > start
            ? output[(start + 1)..end]
            : output[index..].Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
    }

    /// <summary>
    /// Parses the new state reported by <c>pm disable-user</c>/<c>pm enable</c>, for example
    /// <c>Package com.example new state: disabled-user</c>.
    /// </summary>
    public static PackageState? ParseNewStateReport(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        const string marker = "new state:";
        var index = output.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var value = output[(index + marker.Length)..]
            .Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim()
            .ToLowerInvariant();

        return value switch
        {
            null => null,
            "enabled" => PackageState.Enabled,
            "disabled" or "disabled-user" or "disabled_user" => PackageState.Disabled,
            _ => null,
        };
    }
}
