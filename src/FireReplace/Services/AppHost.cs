using FireReplace.Core.Actions;
using FireReplace.Core.Adb;
using FireReplace.Core.Catalog;
using FireReplace.Core.Configuration;
using FireReplace.Core.Discovery;
using FireReplace.Core.Logging;
using FireReplace.Core.Services;
using FireReplace.Dialogs;
using FireReplace.ViewModels;

namespace FireReplace.Services;

/// <summary>
/// The composition root. FireReplace wires its services by hand instead of pulling in a container:
/// the graph is small, fully explicit, and costs nothing at startup.
/// </summary>
public sealed class AppHost
{
    /// <summary>Builds the whole object graph.</summary>
    /// <param name="applicationDirectory">Directory containing the executable.</param>
    public AppHost(string applicationDirectory)
    {
        Paths = new AppPaths(applicationDirectory);
        Paths.EnsureDirectories();

        ConfigStore = new ConfigStore(Paths.ConfigFile);
        Config = ConfigStore.Load();

        var log = new LogService(Config.MaxLogEntries);
        if (Config.SaveLogsAutomatically)
        {
            log.SetAutoSavePath(Paths.AutoLogFile);
        }

        Log = log;
        Locator = new ToolchainLocator(applicationDirectory);

        Adb = new AdbService(new ProcessRunner(), Log)
        {
            DryRun = Config.DryRun,
            ConnectTimeout = TimeSpan.FromSeconds(Math.Clamp(Config.ConnectTimeoutSeconds, 2, 120)),
            CommandTimeout = TimeSpan.FromSeconds(Math.Clamp(Config.CommandTimeoutSeconds, 5, 600)),
        };

        Dialogs = new DialogService();
        Dialogs.UseDryRunProvider(() => Adb.DryRun);
        Dialogs.UseConfirmationPolicy(_ => Config.ConfirmDestructiveActions);

        Connection = new ConnectionService(Adb, Log);
        DeviceStates = new DeviceStateService(Adb, Log);
        Actions = new ActionEngine(Adb, Log, Dialogs);

        SettingsActions = new SettingsActionService(Adb);
        PackageActions = new PackageActionService(Adb);
        LauncherActions = new LauncherActionService(Adb);
        RecommendedSetup = new RecommendedSetupService(LauncherActions, SettingsActions, PackageActions);
        Backups = new BackupService(Paths.BackupDirectory, Adb, Log);

        RefreshToolchain();

        Shell = new ShellViewModel(this);
    }

    /// <summary>Resolved application folders.</summary>
    public AppPaths Paths { get; }

    /// <summary>Persisted configuration store.</summary>
    public ConfigStore ConfigStore { get; }

    /// <summary>Live configuration. Mutated by the Settings page and saved through <see cref="SaveConfig"/>.</summary>
    public AppConfig Config { get; }

    /// <summary>Application log.</summary>
    public LogService Log { get; }

    /// <summary>adb/APK discovery.</summary>
    public ToolchainLocator Locator { get; }

    /// <summary>Where adb.exe was found.</summary>
    public AdbLocation AdbLocation { get; private set; } = new(null, null, [], []);

    /// <summary>Resolved APK directory, or null.</summary>
    public string? ApkDirectory { get; private set; }

    /// <summary>Resolved APK files for the launcher catalog.</summary>
    public IReadOnlyList<ApkLocation> Apks { get; private set; } = Array.Empty<ApkLocation>();

    /// <summary>The ADB gateway.</summary>
    public AdbService Adb { get; }

    /// <summary>Overlay dialog host, also the confirmation host for the action engine.</summary>
    public DialogService Dialogs { get; }

    /// <summary>Connection lifecycle.</summary>
    public ConnectionService Connection { get; }

    /// <summary>Device snapshot reader.</summary>
    public DeviceStateService DeviceStates { get; }

    /// <summary>The confirm-then-execute engine.</summary>
    public ActionEngine Actions { get; }

    /// <summary>Settings plan builder.</summary>
    public SettingsActionService SettingsActions { get; }

    /// <summary>Package plan builder.</summary>
    public PackageActionService PackageActions { get; }

    /// <summary>Launcher plan builder.</summary>
    public LauncherActionService LauncherActions { get; }

    /// <summary>Composite recommended setup.</summary>
    public RecommendedSetupService RecommendedSetup { get; }

    /// <summary>Local backup store.</summary>
    public BackupService Backups { get; }

    /// <summary>The root view model.</summary>
    public ShellViewModel Shell { get; }

    /// <summary>
    /// Re-runs discovery and pushes the result into the ADB service. Called at startup and whenever
    /// the platform-tools or APK folder setting changes.
    /// </summary>
    public void RefreshToolchain()
    {
        AdbLocation = Locator.LocateAdb(Config.PlatformToolsDirectory);
        Adb.AdbPath = AdbLocation.AdbPath;

        ApkDirectory = Locator.LocateApkDirectory(Config.ApkDirectory);
        Apks = Locator.LocateApks(ApkDirectory, LauncherCatalog.ApkDefinitions());
    }

    /// <summary>Applies configuration values that other services cache.</summary>
    public void ApplyConfig()
    {
        Adb.DryRun = Config.DryRun;
        Adb.ConnectTimeout = TimeSpan.FromSeconds(Math.Clamp(Config.ConnectTimeoutSeconds, 2, 120));
        Adb.CommandTimeout = TimeSpan.FromSeconds(Math.Clamp(Config.CommandTimeoutSeconds, 5, 600));
        Log.MaxEntries = Config.MaxLogEntries;
        Log.SetAutoSavePath(Config.SaveLogsAutomatically ? Paths.AutoLogFile : null);
        ThemeManager.Apply(Config.Theme);
        RefreshToolchain();
    }

    /// <summary>Persists the configuration, ignoring transient write failures.</summary>
    public void SaveConfig() => ConfigStore.Save(Config);

    /// <summary>Persists the configuration asynchronously.</summary>
    public Task SaveConfigAsync(CancellationToken cancellationToken = default) =>
        ConfigStore.SaveAsync(Config, cancellationToken);
}
