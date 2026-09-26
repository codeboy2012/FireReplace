using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using FireReplace.Core.Configuration;
using FireReplace.Core.Services;
using FireReplace.Dialogs;
using FireReplace.Mvvm;
using FireReplace.Services;

namespace FireReplace.ViewModels;

/// <summary>
/// The application settings page. Every change is applied and persisted immediately; nothing here
/// touches the Fire TV.
/// </summary>
public sealed class PreferencesViewModel : PageViewModel
{
    private string _toolchainSummary = string.Empty;

    /// <summary>Creates the page.</summary>
    public PreferencesViewModel(AppHost host)
        : base(host)
    {
        BrowsePlatformToolsCommand = new RelayCommand(BrowsePlatformTools);
        BrowseApkFolderCommand = new RelayCommand(BrowseApkFolder);
        ClearPlatformToolsCommand = new RelayCommand(() => PlatformToolsDirectory = string.Empty);
        ClearApkFolderCommand = new RelayCommand(() => ApkDirectory = string.Empty);
        OpenConfigFolderCommand = new RelayCommand(() => OpenFolder(Host.Paths.ConfigDirectory));
        OpenLogFolderCommand = new RelayCommand(() => OpenFolder(Host.Paths.LogDirectory));
        ShowFirstRunAgainCommand = new RelayCommand(ShowFirstRunAgain);

        UpdateToolchainSummary();
    }

    /// <summary>Raised when the user asks to see the first-run checklist again.</summary>
    public event EventHandler? FirstRunRequested;

    /// <inheritdoc />
    public override string Title => "Settings";

    /// <inheritdoc />
    public override string Subtitle => "FireReplace's own preferences. None of these change anything on your Fire TV.";

    private AppConfig Config => Host.Config;

    /// <summary>Whether FireReplace tries to connect at startup using the remembered address.</summary>
    public bool AutoConnectOnStartup
    {
        get => Config.AutoConnectOnStartup;
        set => Set(value, v => Config.AutoConnectOnStartup = v);
    }

    /// <summary>Whether the last used IP address is remembered.</summary>
    public bool RememberLastIp
    {
        get => Config.RememberLastIp;
        set => Set(value, v =>
        {
            Config.RememberLastIp = v;
            if (!v)
            {
                Config.LastIpAddress = null;
            }
        });
    }

    /// <summary>The remembered address, shown read-only so the user can see what is stored.</summary>
    public string LastIpAddress => Config.LastIpAddress ?? "(none stored)";

    /// <summary>True when the dark theme is selected.</summary>
    public bool IsDarkTheme
    {
        get => Config.Theme == AppTheme.Dark;
        set { if (value) { SetTheme(AppTheme.Dark); } }
    }

    /// <summary>True when the midnight theme is selected.</summary>
    public bool IsMidnightTheme
    {
        get => Config.Theme == AppTheme.Midnight;
        set { if (value) { SetTheme(AppTheme.Midnight); } }
    }

    /// <summary>Override for the platform-tools folder.</summary>
    public string PlatformToolsDirectory
    {
        get => Config.PlatformToolsDirectory ?? string.Empty;
        set => Set(value, v =>
        {
            Config.PlatformToolsDirectory = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            Host.RefreshToolchain();
            UpdateToolchainSummary();
        });
    }

    /// <summary>Override for the APK folder.</summary>
    public string ApkDirectory
    {
        get => Config.ApkDirectory ?? string.Empty;
        set => Set(value, v =>
        {
            Config.ApkDirectory = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            Host.RefreshToolchain();
            UpdateToolchainSummary();
        });
    }

    /// <summary>Where adb and the APKs were actually found.</summary>
    public string ToolchainSummary
    {
        get => _toolchainSummary;
        private set => SetProperty(ref _toolchainSummary, value);
    }

    /// <summary>ADB TCP port.</summary>
    public int AdbPort
    {
        get => Config.AdbPort;
        set => Set(Math.Clamp(value, 1, 65535), v => Config.AdbPort = v);
    }

    /// <summary>Connection timeout in seconds.</summary>
    public int ConnectTimeoutSeconds
    {
        get => Config.ConnectTimeoutSeconds;
        set => Set(Math.Clamp(value, 2, 120), v =>
        {
            Config.ConnectTimeoutSeconds = v;
            Host.Adb.ConnectTimeout = TimeSpan.FromSeconds(v);
        });
    }

    /// <summary>Command timeout in seconds.</summary>
    public int CommandTimeoutSeconds
    {
        get => Config.CommandTimeoutSeconds;
        set => Set(Math.Clamp(value, 5, 600), v =>
        {
            Config.CommandTimeoutSeconds = v;
            Host.Adb.CommandTimeout = TimeSpan.FromSeconds(v);
        });
    }

    /// <summary>Whether every modification shows a confirmation panel.</summary>
    public bool ConfirmDestructiveActions
    {
        get => Config.ConfirmDestructiveActions;
        set => Set(value, v => Config.ConfirmDestructiveActions = v);
    }

    /// <summary>Whether advanced/experimental actions need a second confirmation.</summary>
    public bool ConfirmAdvancedActions
    {
        get => Config.ConfirmAdvancedActions;
        set => Set(value, v => Config.ConfirmAdvancedActions = v);
    }

    /// <summary>Whether FireReplace suggests a backup before the first change of a session.</summary>
    public bool OfferBackupBeforeChanges
    {
        get => Config.OfferBackupBeforeChanges;
        set => Set(value, v => Config.OfferBackupBeforeChanges = v);
    }

    /// <summary>When on, no command that would modify the device is executed.</summary>
    public bool DryRun
    {
        get => Config.DryRun;
        set => Set(value, v =>
        {
            Config.DryRun = v;
            Host.Adb.DryRun = v;
            OnPropertyChanged(nameof(IsDryRun));
        });
    }

    /// <summary>Maximum retained log entries.</summary>
    public int MaxLogEntries
    {
        get => Config.MaxLogEntries;
        set => Set(Math.Clamp(value, 100, 100_000), v =>
        {
            Config.MaxLogEntries = v;
            Host.Log.MaxEntries = v;
        });
    }

    /// <summary>Whether log entries are appended to a file as they happen.</summary>
    public bool SaveLogsAutomatically
    {
        get => Config.SaveLogsAutomatically;
        set => Set(value, v =>
        {
            Config.SaveLogsAutomatically = v;
            Host.Log.SetAutoSavePath(v ? Host.Paths.AutoLogFile : null);
        });
    }

    /// <summary>Where automatic logs are written.</summary>
    public string AutoLogPath => Host.Paths.AutoLogFile;

    /// <summary>Picks the platform-tools folder.</summary>
    public RelayCommand BrowsePlatformToolsCommand { get; }

    /// <summary>Picks the APK folder.</summary>
    public RelayCommand BrowseApkFolderCommand { get; }

    /// <summary>Clears the platform-tools override.</summary>
    public RelayCommand ClearPlatformToolsCommand { get; }

    /// <summary>Clears the APK folder override.</summary>
    public RelayCommand ClearApkFolderCommand { get; }

    /// <summary>Opens the configuration folder.</summary>
    public RelayCommand OpenConfigFolderCommand { get; }

    /// <summary>Opens the log folder.</summary>
    public RelayCommand OpenLogFolderCommand { get; }

    /// <summary>Shows the first-run safety checklist again.</summary>
    public RelayCommand ShowFirstRunAgainCommand { get; }

    /// <inheritdoc />
    public override void OnDeviceStateChanged(DeviceState state)
    {
        base.OnDeviceStateChanged(state);
        UpdateToolchainSummary();
    }

    private void SetTheme(AppTheme theme)
    {
        if (Config.Theme == theme)
        {
            return;
        }

        Config.Theme = theme;
        ThemeManager.Apply(theme);
        Host.SaveConfig();
        OnPropertiesChanged(nameof(IsDarkTheme), nameof(IsMidnightTheme));
    }

    private void Set<T>(T value, Action<T> apply, string? propertyName = null)
    {
        apply(value);
        Host.SaveConfig();
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(LastIpAddress));
    }

    private void UpdateToolchainSummary()
    {
        var adb = Host.AdbLocation.AdbPath ?? "adb.exe was not found";
        var apks = Host.Apks.Count == 0
            ? "no APKs found"
            : string.Join(", ", Host.Apks.Select(a => a.Found ? a.DisplayName : $"{a.DisplayName} (missing)"));

        ToolchainSummary = $"adb: {adb}{System.Environment.NewLine}apks: {Host.ApkDirectory ?? "folder not found"} — {apks}";
    }

    private void BrowsePlatformTools()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select adb.exe",
            Filter = "adb.exe|adb.exe|Executables (*.exe)|*.exe",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() == true)
        {
            PlatformToolsDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            OnPropertyChanged(nameof(PlatformToolsDirectory));
        }
    }

    private void BrowseApkFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select the folder that holds your APK files",
        };

        if (dialog.ShowDialog() == true)
        {
            ApkDirectory = dialog.FolderName;
            OnPropertyChanged(nameof(ApkDirectory));
        }
    }

    private void ShowFirstRunAgain()
    {
        Config.FirstRunCompleted = false;
        Host.SaveConfig();
        FirstRunRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Host.Log.Write(Core.Logging.LogLevel.Warning, "app", $"Could not open {path}", ex.Message);
        }
    }
}
