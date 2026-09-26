using FireReplace.Core.Actions;
using FireReplace.Core.Adb;
using FireReplace.Core.Catalog;
using FireReplace.Core.Models;

namespace FireReplace.Core.Services;

/// <summary>
/// Builds package enable/disable plans, refusing guarded packages before a plan is even created.
/// </summary>
public sealed class PackageActionService
{
    private readonly IAdbService _adb;

    /// <summary>Creates the service.</summary>
    public PackageActionService(IAdbService adb) => _adb = adb ?? throw new ArgumentNullException(nameof(adb));

    /// <summary>
    /// Builds a plan that disables the given packages for user 0. Guarded packages are dropped and
    /// reported through <paramref name="refused"/> rather than silently included.
    /// </summary>
    public ActionPlan BuildDisablePlan(
        IEnumerable<string> packageNames,
        DeviceState state,
        out IReadOnlyList<string> refused)
    {
        ArgumentNullException.ThrowIfNull(packageNames);
        ArgumentNullException.ThrowIfNull(state);

        var names = packageNames.Distinct(StringComparer.Ordinal).ToArray();
        var guarded = names.Where(PackageCatalog.IsGuarded).ToArray();
        refused = guarded;

        // Packages that are not on this device are dropped: attempting them would only produce
        // "Unknown package" noise, and package names legitimately differ between Fire OS builds.
        var allowed = names
            .Except(guarded, StringComparer.Ordinal)
            .Where(name => state.StateOf(name) != PackageState.NotInstalled)
            .ToArray();

        var changes = allowed.Select(name => BuildDisableChange(name, state)).ToArray();

        var title = changes.Length == 1 ? "Disable package" : $"Disable {changes.Length} packages";

        return new ActionPlan(
            title,
            changes.Length == 1
                ? "The package is disabled for user 0. It stays installed and can be re-enabled at any time."
                : "Each package is disabled for user 0 with pm disable-user. Nothing is uninstalled, and every package can be re-enabled later.",
            changes,
            Warning: guarded.Length > 0
                ? $"{guarded.Length} protected package(s) were removed from this plan: {string.Join(", ", guarded)}"
                : null,
            ConfirmLabel: changes.Length == 1 ? "Disable" : $"Disable {changes.Length}",
            IsDestructive: true);
    }

    /// <summary>Builds a plan that re-enables the given packages.</summary>
    public ActionPlan BuildEnablePlan(IEnumerable<string> packageNames, DeviceState state)
    {
        ArgumentNullException.ThrowIfNull(packageNames);
        ArgumentNullException.ThrowIfNull(state);

        var changes = packageNames
            .Distinct(StringComparer.Ordinal)
            .Select(name => BuildEnableChange(name, state))
            .ToArray();

        return new ActionPlan(
            changes.Length == 1 ? "Enable package" : $"Enable {changes.Length} packages",
            "The package is re-enabled with pm enable and becomes available to the system again.",
            changes,
            ConfirmLabel: "Enable");
    }

    /// <summary>Builds the change that disables one package.</summary>
    public PlannedChange BuildDisableChange(string packageName, DeviceState state)
    {
        if (PackageCatalog.IsGuarded(packageName))
        {
            throw new ProtectedPackageException(packageName);
        }

        var current = state.StateOf(packageName);
        var preview = AdbCommandResult.Format(AdbArguments.DisablePackage(_adb.Serial, packageName));

        return new PlannedChange(
            ChangeKind.PackageDisable,
            "Disable package",
            packageName,
            Describe(current),
            "Disabled",
            ct => _adb.DisablePackageAsync(packageName, ct),
            preview,
            PackageCatalog.DescriptionFor(packageName));
    }

    /// <summary>Builds the change that enables one package.</summary>
    public PlannedChange BuildEnableChange(string packageName, DeviceState state)
    {
        var current = state.StateOf(packageName);
        var preview = AdbCommandResult.Format(AdbArguments.EnablePackage(_adb.Serial, packageName));

        return new PlannedChange(
            ChangeKind.PackageEnable,
            "Enable package",
            packageName,
            Describe(current),
            "Enabled",
            ct => _adb.EnablePackageAsync(packageName, ct),
            preview,
            PackageCatalog.DescriptionFor(packageName));
    }

    /// <summary>
    /// The verified debloat packages that are present on this device and not already disabled.
    /// </summary>
    public IReadOnlyList<PackageInfo> DebloatCandidates(DeviceState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return PackageCatalog.VerifiedDebloatNames.Select(state.PackageInfoFor).ToArray();
    }

    private static string Describe(PackageState state) => state switch
    {
        PackageState.Enabled => "Enabled",
        PackageState.Disabled => "Disabled",
        PackageState.NotInstalled => "Not installed",
        _ => "Unknown",
    };
}
