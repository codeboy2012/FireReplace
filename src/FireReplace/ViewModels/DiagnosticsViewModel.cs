using System.IO;
using System.Collections.ObjectModel;
using System.Diagnostics;
using FireReplace.Core.Actions;
using FireReplace.Core.Adb;
using FireReplace.Core.Services;
using FireReplace.Dialogs;
using FireReplace.Mvvm;
using FireReplace.Services;

namespace FireReplace.ViewModels;

/// <summary>A label/value pair shown in the diagnostics tables.</summary>
/// <param name="Label">Field name.</param>
/// <param name="Value">Field value as reported by the device or the environment.</param>
public sealed record DiagnosticFact(string Label, string Value);

/// <summary>
/// Diagnostics and backup management: what FireReplace found, what the device reports, and the local
/// configuration backups.
/// </summary>
public sealed class DiagnosticsViewModel : PageViewModel
{
    private string _adbVersion = "not checked";
    private string _deviceListing = "not checked";

    /// <summary>Creates the page.</summary>
    public DiagnosticsViewModel(AppHost host)
        : base(host)
    {
        RunChecksCommand = new AsyncRelayCommand(RunChecksAsync);
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, () => State is not null);
        RestoreCommand = new AsyncRelayCommand((parameter, ct) => RestoreAsync(parameter as BackupEntry, ct));
        OpenBackupFolderCommand = new RelayCommand(OpenBackupFolder);
        RebootCommand = new AsyncRelayCommand(RebootAsync, () => Host.Connection.Status == ConnectionStatus.Connected);

        ReloadBackups();
        BuildEnvironmentFacts();
    }

    /// <inheritdoc />
    public override string Title => "Diagnostics";

    /// <inheritdoc />
    public override string Subtitle =>
        "What FireReplace found on this PC, what the Fire TV reports, and your local configuration backups.";

    /// <summary>Environment facts: paths, timeouts, discovery results.</summary>
    public ObservableCollection<DiagnosticFact> Environment { get; } = [];

    /// <summary>Device facts read from getprop.</summary>
    public ObservableCollection<DiagnosticFact> Device { get; } = [];

    /// <summary>Available backups, newest first.</summary>
    public ObservableCollection<BackupEntry> Backups { get; } = [];

    /// <summary>Reported adb client version.</summary>
    public string AdbVersion
    {
        get => _adbVersion;
        private set => SetProperty(ref _adbVersion, value);
    }

    /// <summary>Raw <c>adb devices</c> output.</summary>
    public string DeviceListing
    {
        get => _deviceListing;
        private set => SetProperty(ref _deviceListing, value);
    }

    /// <summary>Runs the environment and connection checks.</summary>
    public AsyncRelayCommand RunChecksCommand { get; }

    /// <summary>Creates a configuration backup.</summary>
    public AsyncRelayCommand CreateBackupCommand { get; }

    /// <summary>Restores the backup passed as the command parameter.</summary>
    public AsyncRelayCommand RestoreCommand { get; }

    /// <summary>Opens the backup folder in Explorer.</summary>
    public RelayCommand OpenBackupFolderCommand { get; }

    /// <summary>Reboots the Fire TV after confirmation.</summary>
    public AsyncRelayCommand RebootCommand { get; }

    /// <inheritdoc />
    public override void OnDeviceStateChanged(DeviceState state)
    {
        base.OnDeviceStateChanged(state);

        Device.Clear();
        var device = state.Device;
        if (device is null)
        {
            Device.Add(new DiagnosticFact("Device", "No information has been read yet."));
        }
        else
        {
            Device.Add(new DiagnosticFact("Serial", device.Serial));
            Device.Add(new DiagnosticFact("Model (ro.product.model)", device.Model ?? "—"));
            Device.Add(new DiagnosticFact("Product (ro.build.product)", device.Product ?? "—"));
            Device.Add(new DiagnosticFact("Manufacturer", device.Manufacturer ?? "—"));
            Device.Add(new DiagnosticFact("Android release", device.AndroidRelease ?? "—"));
            Device.Add(new DiagnosticFact("SDK level", device.SdkLevel ?? "—"));
            Device.Add(new DiagnosticFact("Build (incremental)", device.BuildIncremental ?? "—"));
            Device.Add(new DiagnosticFact("Fire OS / build name", device.FireOsVersion ?? "—"));
            Device.Add(new DiagnosticFact("CPU ABI", device.Abi ?? "—"));
            Device.Add(new DiagnosticFact("Installed packages", state.InstalledPackages.Count.ToString()));
            Device.Add(new DiagnosticFact("Disabled packages", state.DisabledPackages.Count.ToString()));
            Device.Add(new DiagnosticFact(
                "Accessibility services",
                state.AccessibilityServices.Count == 0 ? "(none)" : string.Join(", ", state.AccessibilityServices)));
            Device.Add(new DiagnosticFact(
                "Notification listeners",
                state.NotificationListeners.Count == 0 ? "(none)" : string.Join(", ", state.NotificationListeners)));
        }

        BuildEnvironmentFacts();
        CreateBackupCommand.RaiseCanExecuteChanged();
        RebootCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Re-reads the backup folder.</summary>
    public void ReloadBackups()
    {
        Backups.Clear();
        foreach (var entry in Host.Backups.List())
        {
            Backups.Add(entry);
        }
    }

    private void BuildEnvironmentFacts()
    {
        Environment.Clear();
        Environment.Add(new DiagnosticFact("FireReplace folder", Host.Paths.ApplicationDirectory));
        Environment.Add(new DiagnosticFact("adb.exe", Host.AdbLocation.AdbPath ?? "not found"));
        Environment.Add(new DiagnosticFact(
            "Windows USB libraries",
            Host.AdbLocation.MissingSupportLibraries.Count == 0
                ? "present"
                : "missing: " + string.Join(", ", Host.AdbLocation.MissingSupportLibraries)));
        Environment.Add(new DiagnosticFact("APK folder", Host.ApkDirectory ?? "not found"));
        Environment.Add(new DiagnosticFact(
            "APKs",
            Host.Apks.Count == 0
                ? "none"
                : string.Join(", ", Host.Apks.Select(a => $"{a.DisplayName}: {(a.Found ? a.FileName : "missing")}"))));
        Environment.Add(new DiagnosticFact("Backups", Host.Paths.BackupDirectory));
        Environment.Add(new DiagnosticFact("Settings file", Host.Paths.ConfigFile));
        Environment.Add(new DiagnosticFact("Connect timeout", $"{Host.Adb.ConnectTimeout.TotalSeconds:0}s"));
        Environment.Add(new DiagnosticFact("Command timeout", $"{Host.Adb.CommandTimeout.TotalSeconds:0}s"));
        Environment.Add(new DiagnosticFact("Dry run", Host.Adb.DryRun ? "ON — no changes will be written" : "off"));
    }

    private async Task RunChecksAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            if (!Host.AdbLocation.Found)
            {
                AdbVersion = "adb.exe was not found.";
                DeviceListing = "—";
                return;
            }

            var version = await Host.Adb.GetVersionAsync(cancellationToken).ConfigureAwait(true);
            AdbVersion = version.Success
                ? version.Output.Split('\n').FirstOrDefault()?.Trim() ?? "unknown"
                : version.CombinedOutput;

            var devices = await Host.Adb.GetDevicesAsync(cancellationToken).ConfigureAwait(true);
            DeviceListing = devices.Count == 0
                ? "adb reported no devices."
                : string.Join(
                    System.Environment.NewLine,
                    devices.Select(d => $"{d.Serial}\t{d.RawState}"));

            BuildEnvironmentFacts();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CreateBackupAsync(CancellationToken cancellationToken)
    {
        if (State is not { } state)
        {
            return;
        }

        var entry = await Host.Backups.CreateAsync(state, cancellationToken).ConfigureAwait(true);
        ReloadBackups();

        await Dialogs.ShowInfoAsync(
            "Backup created",
            $"Saved {entry.SettingCount} setting value(s) and {entry.PackageCount} package state(s).",
            entry.Directory,
            MessageSeverity.Success).ConfigureAwait(true);
    }

    private async Task RestoreAsync(BackupEntry? entry, CancellationToken cancellationToken)
    {
        if (entry is null)
        {
            return;
        }

        if (State is not { } state)
        {
            await Dialogs.ShowInfoAsync(
                "Not connected",
                "Connect to a Fire TV before restoring a backup.",
                severity: MessageSeverity.Warning).ConfigureAwait(true);
            return;
        }

        ActionPlan plan;
        try
        {
            plan = Host.Backups.BuildRestorePlan(entry, state);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            await Dialogs.ShowInfoAsync("Backup unreadable", ex.Message, severity: MessageSeverity.Error)
                .ConfigureAwait(true);
            return;
        }

        if (plan.IsEmpty)
        {
            await Dialogs.ShowInfoAsync(
                "Nothing to restore",
                "The device already matches this backup.",
                entry.DisplayLabel).ConfigureAwait(true);
            return;
        }

        await RunPlanAsync(plan, RefreshScope.All, cancellationToken).ConfigureAwait(true);
    }

    private void OpenBackupFolder()
    {
        try
        {
            Directory.CreateDirectory(Host.Paths.BackupDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = Host.Paths.BackupDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Host.Log.Write(Core.Logging.LogLevel.Warning, "app", "Could not open the backup folder.", ex.Message);
        }
    }

    private async Task RebootAsync(CancellationToken cancellationToken)
    {
        var plan = new ActionPlan(
            "Reboot the Fire TV",
            "The TV restarts immediately. FireReplace will disconnect; reconnect once the TV is back on the home screen.",
            [
                new PlannedChange(
                    ChangeKind.Reboot,
                    "Reboot",
                    Host.Connection.Serial ?? "device",
                    "Running",
                    "Rebooting",
                    ct => Host.Adb.RebootAsync(ct),
                    AdbCommandResult.Format(AdbArguments.Reboot(Host.Adb.Serial))),
            ],
            ConfirmLabel: "Reboot",
            IsDestructive: true);

        var report = await Host.Actions.RunAsync(plan, cancellationToken).ConfigureAwait(true);

        if (report?.AllSucceeded == true && !report.WasDryRun)
        {
            await Host.Connection.DisconnectAsync(cancellationToken).ConfigureAwait(true);
            Host.DeviceStates.Reset();
        }
    }
}
