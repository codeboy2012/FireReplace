using FireReplace.Core.Actions;
using FireReplace.Core.Catalog;
using FireReplace.Core.Discovery;

namespace FireReplace.Core.Services;

/// <summary>
/// Composes the verified workflow into one reviewable plan: install the launcher apps, grant the
/// permission Home on Fire needs, add the Projectivy accessibility service without removing Home on
/// Fire's, apply the animation and privacy settings, and disable the verified package list.
/// </summary>
/// <remarks>
/// Notification access and every experimental setting are deliberately excluded. Those are opt-in on
/// their own pages so the user chooses them consciously.
/// </remarks>
public sealed class RecommendedSetupService
{
    private readonly LauncherActionService _launchers;
    private readonly SettingsActionService _settings;
    private readonly PackageActionService _packages;

    /// <summary>Creates the service.</summary>
    public RecommendedSetupService(
        LauncherActionService launchers,
        SettingsActionService settings,
        PackageActionService packages)
    {
        _launchers = launchers ?? throw new ArgumentNullException(nameof(launchers));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
    }

    /// <summary>What the recommended setup will never do, shown verbatim in the confirmation panel.</summary>
    public static IReadOnlyList<string> Exclusions { get; } =
    [
        "Bypass packages that Fire OS protects",
        "Disable the stock Fire TV launcher, AirPlay, connectivity, Alexa or OTA updates",
        "Enable notification access (that stays opt-in on the Launcher page)",
        "Apply any experimental or unverified setting",
        "Root, unlock, flash, wipe or factory reset the TV",
    ];

    /// <summary>Builds the full plan from the current snapshot and the APKs that were found.</summary>
    public ActionPlan BuildPlan(DeviceState state, IReadOnlyList<ApkLocation> apks)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(apks);

        var changes = new List<PlannedChange>();

        foreach (var app in LauncherCatalog.All)
        {
            var apk = apks.FirstOrDefault(a => a.PackageName == app.PackageName);
            if (apk?.Path is not null)
            {
                changes.Add(_launchers.BuildInstallChange(app, apk, state));
            }
        }

        foreach (var permission in LauncherCatalog.HomeOnFire.Permissions)
        {
            changes.Add(_launchers.BuildGrantChange(LauncherCatalog.HomeOnFire, permission));
        }

        var accessibilityPlan = _launchers.BuildAccessibilityPlan(state, LauncherCatalog.KnownAccessibilityServices);
        changes.AddRange(accessibilityPlan.Changes);

        changes.AddRange(_settings.BuildChanges(SettingsCatalog.VerifiedGroups, state));

        var debloatPlan = _packages.BuildDisablePlan(PackageCatalog.VerifiedDebloatNames, state, out _);
        changes.AddRange(debloatPlan.Changes);

        var description = string.Join(
            System.Environment.NewLine,
            [
                "This runs the verified workflow end to end:",
                string.Empty,
                "  \u2022 Install Home on Fire and Projectivy Launcher from the local apks folder",
                "  \u2022 Grant Home on Fire WRITE_SECURE_SETTINGS",
                "  \u2022 Add the Projectivy accessibility service, preserving Home on Fire's",
                "  \u2022 Set the three animation scales to 0",
                "  \u2022 Apply the three verified privacy and advertising settings",
                "  \u2022 Disable the verified package list",
                string.Empty,
                "It will NOT:",
                string.Empty,
                .. Exclusions.Select(e => $"  \u2022 {e}"),
            ]);

        return new ActionPlan(
            "Recommended setup",
            description,
            changes,
            ConfirmLabel: "Run setup",
            IsDestructive: true);
    }
}
