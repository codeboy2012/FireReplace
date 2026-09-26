using FireReplace.Core.Discovery;

namespace FireReplace.Core.Catalog;

/// <summary>A third-party launcher/helper app FireReplace knows how to set up.</summary>
/// <param name="Id">Stable identifier.</param>
/// <param name="DisplayName">Name shown in the UI.</param>
/// <param name="PackageName">Package name on the device.</param>
/// <param name="ApkFilePattern">Glob matched inside the configured APK directory.</param>
/// <param name="Summary">One-line description.</param>
/// <param name="AccessibilityService">Accessibility component the app needs, when any.</param>
/// <param name="NotificationListener">Notification-listener component the app can use, when any.</param>
/// <param name="RequiredPermissions">Permissions FireReplace can grant via <c>pm grant</c>.</param>
/// <param name="LaunchAction">Optional intent action used to open the app's own settings.</param>
public sealed record LauncherApp(
    string Id,
    string DisplayName,
    string PackageName,
    string ApkFilePattern,
    string Summary,
    string? AccessibilityService = null,
    string? NotificationListener = null,
    IReadOnlyList<string>? RequiredPermissions = null,
    string? LaunchAction = null)
{
    /// <summary>Permissions, never null.</summary>
    public IReadOnlyList<string> Permissions => RequiredPermissions ?? Array.Empty<string>();
}

/// <summary>The launcher apps supported by the original tested workflow.</summary>
public static class LauncherCatalog
{
    /// <summary>Home on Fire — intercepts the Home button so a replacement launcher can take over.</summary>
    public static readonly LauncherApp HomeOnFire = new(
        Id: "home-on-fire",
        DisplayName: "Home on Fire",
        PackageName: "io.github.toolicious.homeonfire",
        ApkFilePattern: "home-on-fire*.apk",
        Summary: "Redirects the Fire TV Home button to a launcher of your choice.",
        AccessibilityService: "io.github.toolicious.homeonfire/.HijackService",
        RequiredPermissions: ["android.permission.WRITE_SECURE_SETTINGS"]);

    /// <summary>Projectivy Launcher — the replacement launcher itself.</summary>
    public static readonly LauncherApp Projectivy = new(
        Id: "projectivy",
        DisplayName: "Projectivy Launcher",
        PackageName: "com.spocky.projengmenu",
        ApkFilePattern: "Projectivy*.apk",
        Summary: "A fast, highly customisable Android TV launcher.",
        AccessibilityService: "com.spocky.projengmenu/.services.ProjectivyAccessibilityService",
        NotificationListener: "com.spocky.projengmenu/.services.notification.NotificationListener");

    /// <summary>All supported launcher apps.</summary>
    public static readonly IReadOnlyList<LauncherApp> All = [HomeOnFire, Projectivy];

    /// <summary>
    /// Accessibility services that must be preserved when FireReplace rewrites the accessibility list.
    /// </summary>
    public static IReadOnlyList<string> KnownAccessibilityServices { get; } =
        All.Where(a => a.AccessibilityService is not null).Select(a => a.AccessibilityService!).ToArray();

    /// <summary>Converts the catalog into unresolved APK locations for the locator.</summary>
    public static IReadOnlyList<ApkLocation> ApkDefinitions() =>
        All.Select(a => new ApkLocation(a.DisplayName, a.ApkFilePattern, a.PackageName, null)).ToArray();

    /// <summary>Finds a launcher app by package name.</summary>
    public static LauncherApp? ByPackage(string packageName) =>
        All.FirstOrDefault(a => string.Equals(a.PackageName, packageName, StringComparison.Ordinal));

    /// <summary>Friendly name for a known accessibility/notification component, or the raw value.</summary>
    public static string FriendlyComponentName(string component)
    {
        var slash = component.IndexOf('/');
        var package = slash > 0 ? component[..slash] : component;
        return ByPackage(package)?.DisplayName ?? component;
    }
}
