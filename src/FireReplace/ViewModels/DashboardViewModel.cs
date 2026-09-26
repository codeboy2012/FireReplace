using FireReplace.Core.Catalog;
using FireReplace.Core.Models;
using FireReplace.Core.Services;
using FireReplace.Dialogs;
using FireReplace.Mvvm;
using FireReplace.Services;

namespace FireReplace.ViewModels;

/// <summary>
/// The dashboard. Everything it shows comes from the cached snapshot, so switching to this page
/// costs nothing. It only talks to the device when Refresh is pressed or after an action.
/// </summary>
public sealed class DashboardViewModel : PageViewModel
{
    private string _deviceName = "—";
    private string _deviceModel = "—";
    private string _deviceAndroid = "—";
    private string _deviceBuild = "—";
    private string _connectionState = "Not connected";
    private string _connectionSerial = "—";
    private string _animationState = "Unknown";
    private string _privacyState = "Unknown";
    private string _debloatState = "Unknown";
    private string _launcherState = "Unknown";
    private string _lastRefreshed = "never";
    private string? _backupHint;

    /// <summary>Creates the view model.</summary>
    public DashboardViewModel(AppHost host)
        : base(host)
    {
        RefreshCommand = new AsyncRelayCommand(ct => RefreshAsync(RefreshScope.All, ct));
        RecommendedSetupCommand = new AsyncRelayCommand(RunRecommendedSetupAsync, () => State is not null);
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, () => State is not null);
    }

    /// <inheritdoc />
    public override string Title => "Dashboard";

    /// <inheritdoc />
    public override string Subtitle => "Everything FireReplace currently knows about your Fire TV.";

    /// <summary>Device friendly name.</summary>
    public string DeviceName { get => _deviceName; private set => SetProperty(ref _deviceName, value); }

    /// <summary>Amazon model identifier.</summary>
    public string DeviceModel { get => _deviceModel; private set => SetProperty(ref _deviceModel, value); }

    /// <summary>Android release.</summary>
    public string DeviceAndroid { get => _deviceAndroid; private set => SetProperty(ref _deviceAndroid, value); }

    /// <summary>Build / Fire OS information.</summary>
    public string DeviceBuild { get => _deviceBuild; private set => SetProperty(ref _deviceBuild, value); }

    /// <summary>Connection headline.</summary>
    public string ConnectionState { get => _connectionState; private set => SetProperty(ref _connectionState, value); }

    /// <summary>Connected serial.</summary>
    public string ConnectionSerial { get => _connectionSerial; private set => SetProperty(ref _connectionSerial, value); }

    /// <summary>Animation summary.</summary>
    public string AnimationState { get => _animationState; private set => SetProperty(ref _animationState, value); }

    /// <summary>Privacy summary.</summary>
    public string PrivacyState { get => _privacyState; private set => SetProperty(ref _privacyState, value); }

    /// <summary>Debloat summary.</summary>
    public string DebloatState { get => _debloatState; private set => SetProperty(ref _debloatState, value); }

    /// <summary>Launcher summary.</summary>
    public string LauncherState { get => _launcherState; private set => SetProperty(ref _launcherState, value); }

    /// <summary>When the snapshot was taken.</summary>
    public string LastRefreshed { get => _lastRefreshed; private set => SetProperty(ref _lastRefreshed, value); }

    /// <summary>Reminder shown when no backup exists yet.</summary>
    public string? BackupHint { get => _backupHint; private set => SetProperty(ref _backupHint, value); }

    /// <summary>Re-reads the whole snapshot.</summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>Runs the verified workflow end to end after a full preview.</summary>
    public AsyncRelayCommand RecommendedSetupCommand { get; }

    /// <summary>Writes a configuration backup.</summary>
    public AsyncRelayCommand CreateBackupCommand { get; }

    /// <inheritdoc />
    public override void OnDeviceStateChanged(DeviceState state)
    {
        base.OnDeviceStateChanged(state);

        var device = state.Device;
        DeviceName = device?.DisplayName ?? "Unknown device";
        DeviceModel = device?.Model ?? "—";
        DeviceAndroid = device?.AndroidRelease is { } release ? $"Android {release}" : "—";
        DeviceBuild = BuildLine(device);

        ConnectionState = Host.Connection.Status == Core.Adb.ConnectionStatus.Connected ? "Connected" : "Not connected";
        ConnectionSerial = Host.Connection.Serial ?? "—";

        AnimationState = state.IsApplied(SettingsCatalog.Animations)
            ? "Instant"
            : SettingsCatalog.Animations.Settings.Any(state.IsApplied)
                ? "Partly applied"
                : "Default";

        var appliedPrivacy = SettingsCatalog.Privacy.Settings.Count(state.IsApplied);
        PrivacyState = appliedPrivacy switch
        {
            0 => "Not applied",
            var n when n == SettingsCatalog.Privacy.Settings.Count => "Limited",
            var n => $"{n} of {SettingsCatalog.Privacy.Settings.Count} applied",
        };

        var present = PackageCatalog.VerifiedDebloatNames
            .Where(p => state.StateOf(p) != PackageState.NotInstalled)
            .ToArray();
        var disabled = present.Count(p => state.StateOf(p) == PackageState.Disabled);
        DebloatState = present.Length == 0
            ? "No verified packages found"
            : $"{disabled} of {present.Length} disabled";

        LauncherState = DescribeLauncher(state);

        LastRefreshed = state.ReadAt.ToString("HH:mm:ss");
        BackupHint = Host.Backups.List().Count == 0
            ? "No backup yet. A backup records the settings and package states FireReplace can change."
            : null;

        RecommendedSetupCommand.RaiseCanExecuteChanged();
        CreateBackupCommand.RaiseCanExecuteChanged();
    }

    private static string BuildLine(Core.Models.DeviceInfo? device)
    {
        if (device is null)
        {
            return "—";
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(device.BuildIncremental))
        {
            parts.Add(device.BuildIncremental!);
        }

        if (!string.IsNullOrWhiteSpace(device.FireOsVersion))
        {
            parts.Add($"Fire OS {device.FireOsVersion}");
        }

        return parts.Count == 0 ? "—" : string.Join("  ·  ", parts);
    }

    private static string DescribeLauncher(DeviceState state)
    {
        var projectivy = state.InstalledPackages.Contains(LauncherCatalog.Projectivy.PackageName);
        var homeOnFire = state.InstalledPackages.Contains(LauncherCatalog.HomeOnFire.PackageName);

        if (projectivy && homeOnFire)
        {
            var hijackActive = state.HasAccessibilityService(LauncherCatalog.HomeOnFire.AccessibilityService!);
            return hijackActive ? "Projectivy (Home button redirected)" : "Projectivy (Home redirect off)";
        }

        if (projectivy)
        {
            return "Projectivy installed";
        }

        if (homeOnFire)
        {
            return "Home on Fire only";
        }

        return "Stock Fire TV launcher";
    }

    private async Task RunRecommendedSetupAsync(CancellationToken cancellationToken)
    {
        if (State is null)
        {
            return;
        }

        var missing = Host.Apks.Where(a => !a.Found).ToArray();
        if (missing.Length > 0)
        {
            await Dialogs.ShowInfoAsync(
                "APK files missing",
                "The recommended setup installs Home on Fire and Projectivy Launcher from the local apks folder.",
                string.Join(
                    System.Environment.NewLine,
                    missing.Select(m => $"Missing: {m.DisplayName} (expected {m.FileNamePattern})")),
                MessageSeverity.Warning).ConfigureAwait(true);
        }

        var plan = Host.RecommendedSetup.BuildPlan(State, Host.Apks);
        await RunPlanAsync(plan, RefreshScope.All, cancellationToken).ConfigureAwait(true);
    }

    private async Task CreateBackupAsync(CancellationToken cancellationToken)
    {
        if (State is null)
        {
            return;
        }

        var entry = await Host.Backups.CreateAsync(State, cancellationToken).ConfigureAwait(true);
        BackupHint = null;

        await Dialogs.ShowInfoAsync(
            "Backup created",
            $"Saved {entry.SettingCount} setting value(s) and {entry.PackageCount} package state(s).",
            entry.Directory,
            MessageSeverity.Success).ConfigureAwait(true);
    }
}
