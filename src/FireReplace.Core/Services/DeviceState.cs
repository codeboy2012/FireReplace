using FireReplace.Core.Catalog;
using FireReplace.Core.Models;

namespace FireReplace.Core.Services;

/// <summary>
/// Everything FireReplace knows about the connected device at one point in time. Built by a single
/// refresh so the whole UI can be updated from one consistent snapshot.
/// </summary>
public sealed class DeviceState
{
    /// <summary>Identity information, or null when it could not be read.</summary>
    public DeviceInfo? Device { get; init; }

    /// <summary>Raw setting values keyed by <c>scope/key</c>. A null value means the key is unset.</summary>
    public IReadOnlyDictionary<string, string?> Settings { get; init; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);

    /// <summary>Enabled accessibility service components, in device order.</summary>
    public IReadOnlyList<string> AccessibilityServices { get; init; } = Array.Empty<string>();

    /// <summary>Enabled notification listener components, in device order.</summary>
    public IReadOnlyList<string> NotificationListeners { get; init; } = Array.Empty<string>();

    /// <summary>All installed package names.</summary>
    public IReadOnlySet<string> InstalledPackages { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Package names reported as disabled.</summary>
    public IReadOnlySet<string> DisabledPackages { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>When the snapshot was taken.</summary>
    public DateTimeOffset ReadAt { get; init; } = DateTimeOffset.Now;

    /// <summary>True when at least the device identity was read successfully.</summary>
    public bool IsPopulated => Device is not null;

    /// <summary>Builds the dictionary key for a setting definition.</summary>
    public static string KeyOf(SettingDefinition definition) => $"{definition.ScopeToken}/{definition.Key}";

    /// <summary>Builds the dictionary key for a scope and key pair.</summary>
    public static string KeyOf(SettingScope scope, string key) =>
        $"{(scope == SettingScope.Global ? "global" : scope == SettingScope.Secure ? "secure" : "system")}/{key}";

    /// <summary>Current value of a setting, or null when unset or unread.</summary>
    public string? ValueOf(SettingDefinition definition) =>
        Settings.TryGetValue(KeyOf(definition), out var value) ? value : null;

    /// <summary>True when the setting already holds its applied value.</summary>
    public bool IsApplied(SettingDefinition definition) =>
        string.Equals(ValueOf(definition), definition.AppliedValue, StringComparison.Ordinal);

    /// <summary>True when every setting in the group already holds its applied value.</summary>
    public bool IsApplied(SettingGroup group) => group.Settings.All(IsApplied);

    /// <summary>Resolves the state of a single package.</summary>
    public PackageState StateOf(string packageName)
    {
        if (DisabledPackages.Contains(packageName))
        {
            return PackageState.Disabled;
        }

        if (InstalledPackages.Contains(packageName))
        {
            return PackageState.Enabled;
        }

        return InstalledPackages.Count == 0 ? PackageState.Unknown : PackageState.NotInstalled;
    }

    /// <summary>Builds a <see cref="PackageInfo"/> for a package, attaching catalog metadata.</summary>
    public PackageInfo PackageInfoFor(string packageName) => new()
    {
        Name = packageName,
        State = StateOf(packageName),
        Classification = PackageCatalog.Classify(packageName),
        Description = PackageCatalog.DescriptionFor(packageName),
    };

    /// <summary>True when the given component is in the accessibility list.</summary>
    public bool HasAccessibilityService(string component) =>
        AccessibilityServices.Contains(component, StringComparer.Ordinal);

    /// <summary>True when the given component is in the notification listener list.</summary>
    public bool HasNotificationListener(string component) =>
        NotificationListeners.Contains(component, StringComparer.Ordinal);
}
