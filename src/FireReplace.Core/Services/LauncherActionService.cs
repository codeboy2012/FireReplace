using FireReplace.Core.Actions;
using FireReplace.Core.Adb;
using FireReplace.Core.Catalog;
using FireReplace.Core.Discovery;

namespace FireReplace.Core.Services;

/// <summary>
/// Builds the launcher setup plans: APK installs, the <c>WRITE_SECURE_SETTINGS</c> grant, the
/// additive accessibility merge and notification access.
/// </summary>
public sealed class LauncherActionService
{
    private readonly IAdbService _adb;

    /// <summary>Creates the service.</summary>
    public LauncherActionService(IAdbService adb) => _adb = adb ?? throw new ArgumentNullException(nameof(adb));

    /// <summary>Builds a plan that installs one APK.</summary>
    public ActionPlan BuildInstallPlan(LauncherApp app, ApkLocation apk, DeviceState state)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(apk);

        if (apk.Path is null)
        {
            throw new InvalidOperationException($"The APK for {app.DisplayName} was not found.");
        }

        return new ActionPlan(
            $"Install {app.DisplayName}",
            $"{app.Summary} FireReplace installs the APK file shown below with adb install -r. No file is downloaded.",
            [BuildInstallChange(app, apk, state)],
            ConfirmLabel: "Install");
    }

    /// <summary>Builds the change that installs one APK.</summary>
    public PlannedChange BuildInstallChange(LauncherApp app, ApkLocation apk, DeviceState state)
    {
        var path = apk.Path ?? throw new InvalidOperationException($"The APK for {app.DisplayName} was not found.");
        var installed = state.InstalledPackages.Contains(app.PackageName);
        var preview = AdbCommandResult.Format(AdbArguments.Install(_adb.Serial, path, grantRuntimePermissions: false));

        return new PlannedChange(
            ChangeKind.ApkInstall,
            $"Install {app.DisplayName}",
            apk.FileName ?? Path.GetFileName(path),
            installed ? "Installed (will be updated in place)" : "Not installed",
            "Installed",
            ct => _adb.InstallApkAsync(path, grantRuntimePermissions: false, ct),
            preview,
            $"Package {app.PackageName}. Source file: {path}");
    }

    /// <summary>Builds a plan that grants an app's required permissions.</summary>
    public ActionPlan BuildGrantPlan(LauncherApp app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Permissions.Count == 0)
        {
            throw new InvalidOperationException($"{app.DisplayName} does not need any granted permissions.");
        }

        var changes = app.Permissions.Select(p => BuildGrantChange(app, p)).ToArray();

        return new ActionPlan(
            $"Grant permissions to {app.DisplayName}",
            "WRITE_SECURE_SETTINGS lets the app change the secure settings it needs to redirect the Home button. "
            + "It is a powerful permission: only grant it to apps you trust.",
            changes,
            ConfirmLabel: "Grant");
    }

    /// <summary>Builds the change that grants one permission.</summary>
    public PlannedChange BuildGrantChange(LauncherApp app, string permission)
    {
        var preview = AdbCommandResult.Format(AdbArguments.GrantPermission(_adb.Serial, app.PackageName, permission));

        return new PlannedChange(
            ChangeKind.PermissionGrant,
            "Grant permission",
            app.PackageName,
            "Not granted (or unknown)",
            permission,
            ct => _adb.GrantPermissionAsync(app.PackageName, permission, ct),
            preview,
            $"{app.DisplayName} requires {permission}.");
    }

    /// <summary>
    /// Works out the additive accessibility merge for the given components without applying it.
    /// The caller shows <see cref="ComponentMergeResult.Current"/> and
    /// <see cref="ComponentMergeResult.Proposed"/> side by side before anything is written.
    /// </summary>
    public ComponentMergeResult PreviewAccessibilityMerge(DeviceState state, IEnumerable<string> componentsToAdd)
    {
        ArgumentNullException.ThrowIfNull(state);
        var currentRaw = string.Join(':', state.AccessibilityServices);
        return ComponentListMerge.Add(currentRaw, componentsToAdd);
    }

    /// <summary>
    /// Builds a plan that adds accessibility services while preserving everything already enabled.
    /// </summary>
    /// <remarks>
    /// The list is read first and rewritten additively. Existing entries — most importantly Home on
    /// Fire's <c>HijackService</c> — are never dropped, and the accessibility master switch is only
    /// turned on, never off.
    /// </remarks>
    public ActionPlan BuildAccessibilityPlan(DeviceState state, IEnumerable<string> componentsToAdd)
    {
        ArgumentNullException.ThrowIfNull(state);

        var merge = PreviewAccessibilityMerge(state, componentsToAdd);
        var changes = new List<PlannedChange>();

        if (merge.HasChanges)
        {
            var preview = AdbCommandResult.Format(AdbArguments.PutSetting(
                _adb.Serial,
                SettingScope.Secure,
                "enabled_accessibility_services",
                merge.ProposedValue));

            changes.Add(new PlannedChange(
                ChangeKind.AccessibilityList,
                "Accessibility services",
                "secure enabled_accessibility_services",
                merge.Current.Count == 0 ? "(none)" : merge.CurrentValue,
                merge.ProposedValue,
                ct => _adb.PutSettingAsync(SettingScope.Secure, "enabled_accessibility_services", merge.ProposedValue, ct),
                preview,
                "Existing services are preserved. Only the missing ones are appended."));

            var masterSwitch = SettingsCatalog.ReadOnlyStatus.First(s => s.Key == "accessibility_enabled");
            if (!string.Equals(state.ValueOf(masterSwitch), "1", StringComparison.Ordinal))
            {
                changes.Add(new PlannedChange(
                    ChangeKind.SettingWrite,
                    "Accessibility master switch",
                    "secure accessibility_enabled",
                    state.ValueOf(masterSwitch) ?? "(not set)",
                    "1",
                    ct => _adb.PutSettingAsync(SettingScope.Secure, "accessibility_enabled", "1", ct),
                    AdbCommandResult.Format(AdbArguments.PutSetting(_adb.Serial, SettingScope.Secure, "accessibility_enabled", "1")),
                    "Accessibility services only run when this master switch is on."));
            }
        }

        var description = BuildAccessibilityDescription(merge);

        return new ActionPlan(
            "Update accessibility services",
            description,
            changes,
            Warning: merge.Rejected.Count > 0
                ? $"{merge.Rejected.Count} component name(s) failed validation and were skipped."
                : null,
            ConfirmLabel: "Apply");
    }

    /// <summary>Builds a plan that removes accessibility services, preserving all others.</summary>
    public ActionPlan BuildAccessibilityRemovalPlan(DeviceState state, IEnumerable<string> componentsToRemove)
    {
        ArgumentNullException.ThrowIfNull(state);

        var currentRaw = string.Join(':', state.AccessibilityServices);
        var merge = ComponentListMerge.Remove(currentRaw, componentsToRemove);
        var changes = new List<PlannedChange>();

        if (merge.Added.Count > 0)
        {
            changes.Add(new PlannedChange(
                ChangeKind.AccessibilityList,
                "Accessibility services",
                "secure enabled_accessibility_services",
                merge.CurrentValue,
                merge.Proposed.Count == 0 ? string.Empty : merge.ProposedValue,
                ct => _adb.PutSettingAsync(SettingScope.Secure, "enabled_accessibility_services", merge.ProposedValue, ct),
                AdbCommandResult.Format(AdbArguments.PutSetting(_adb.Serial, SettingScope.Secure, "enabled_accessibility_services", merge.ProposedValue)),
                "Every other enabled service is left exactly as it is."));
        }

        return new ActionPlan(
            "Remove accessibility service",
            "Removes the selected components and leaves every other enabled service untouched.",
            changes,
            ConfirmLabel: "Remove");
    }

    /// <summary>Builds a plan that grants notification access to a component.</summary>
    public ActionPlan BuildNotificationPlan(LauncherApp app, DeviceState state)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(state);

        var component = app.NotificationListener
            ?? throw new InvalidOperationException($"{app.DisplayName} has no notification listener.");

        var preview = AdbCommandResult.Format(AdbArguments.AllowNotificationListener(_adb.Serial, component));

        var change = new PlannedChange(
            ChangeKind.NotificationListener,
            "Notification access",
            component,
            state.HasNotificationListener(component) ? "Enabled" : "Not enabled",
            "Enabled",
            ct => _adb.AllowNotificationListenerAsync(component, ct),
            preview,
            "A notification listener can read the content of every notification the Fire TV shows, "
            + "including notifications from other apps. Only enable it if you want this app to display them.");

        return new ActionPlan(
            $"Enable notification access for {app.DisplayName}",
            "Notification access lets the app read notifications posted by the system and by other apps. "
            + "FireReplace never enables this without asking. If Fire OS rejects the command, you can enable it "
            + "manually from the TV's notification-listener settings screen instead.",
            [change],
            Warning: "This is a sensitive permission. Only continue if you understand what the app will be able to read.",
            ConfirmLabel: "Enable");
    }

    /// <summary>
    /// Opens the device's notification-listener settings screen so the user can enable access by hand
    /// when the ADB command is refused.
    /// </summary>
    public Task<AdbCommandResult> OpenNotificationSettingsAsync(CancellationToken cancellationToken = default) =>
        _adb.OpenSettingsScreenAsync("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS", cancellationToken);

    private static string BuildAccessibilityDescription(ComponentMergeResult merge)
    {
        var lines = new List<string>
        {
            "Current accessibility services:",
        };

        if (merge.Current.Count == 0)
        {
            lines.Add("  (none)");
        }
        else
        {
            lines.AddRange(merge.Current.Select(c => $"  \u2713 {LauncherCatalog.FriendlyComponentName(c)}"));
        }

        lines.Add(string.Empty);
        lines.Add("Proposed:");

        if (merge.Proposed.Count == 0)
        {
            lines.Add("  (none)");
        }
        else
        {
            lines.AddRange(merge.Proposed.Select(c =>
                $"  \u2713 {LauncherCatalog.FriendlyComponentName(c)}{(merge.Added.Contains(c, StringComparer.Ordinal) ? "   (added)" : string.Empty)}"));
        }

        return string.Join(System.Environment.NewLine, lines);
    }
}
