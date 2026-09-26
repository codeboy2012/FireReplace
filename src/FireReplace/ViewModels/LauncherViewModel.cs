using System.Collections.ObjectModel;
using FireReplace.Core.Catalog;
using FireReplace.Core.Discovery;
using FireReplace.Core.Models;
using FireReplace.Core.Services;
using FireReplace.Dialogs;
using FireReplace.Mvvm;
using FireReplace.Services;

namespace FireReplace.ViewModels;

/// <summary>One launcher app with its install and permission actions.</summary>
public sealed class LauncherAppViewModel : ObservableObject
{
    private string _installState = "Unknown";
    private string _apkState = "Looking for the APK…";
    private bool _apkFound;
    private bool _accessibilityEnabled;

    /// <summary>Creates the row.</summary>
    internal LauncherAppViewModel(
        LauncherApp app,
        Func<LauncherAppViewModel, CancellationToken, Task> install,
        Func<LauncherAppViewModel, CancellationToken, Task> grant)
    {
        App = app;
        InstallCommand = new AsyncRelayCommand(ct => install(this, ct), () => ApkFound);
        GrantCommand = new AsyncRelayCommand(ct => grant(this, ct), () => App.Permissions.Count > 0);
    }

    /// <summary>Catalog entry.</summary>
    public LauncherApp App { get; }

    /// <summary>Display name.</summary>
    public string DisplayName => App.DisplayName;

    /// <summary>Package name.</summary>
    public string PackageName => App.PackageName;

    /// <summary>One-line description.</summary>
    public string Summary => App.Summary;

    /// <summary>True when the app declares permissions FireReplace can grant.</summary>
    public bool HasPermissions => App.Permissions.Count > 0;

    /// <summary>The permission list, joined for display.</summary>
    public string PermissionList => string.Join(", ", App.Permissions);

    /// <summary>Resolved APK location, when found.</summary>
    public ApkLocation? Apk { get; private set; }

    /// <summary>Whether the APK is available.</summary>
    public bool ApkFound
    {
        get => _apkFound;
        private set
        {
            if (SetProperty(ref _apkFound, value))
            {
                InstallCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>File name or the reason the APK is missing.</summary>
    public string ApkState
    {
        get => _apkState;
        private set => SetProperty(ref _apkState, value);
    }

    /// <summary>Install state on the device.</summary>
    public string InstallState
    {
        get => _installState;
        private set => SetProperty(ref _installState, value);
    }

    /// <summary>True when the app's accessibility service is enabled.</summary>
    public bool AccessibilityEnabled
    {
        get => _accessibilityEnabled;
        private set => SetProperty(ref _accessibilityEnabled, value);
    }

    /// <summary>True when the app declares an accessibility service.</summary>
    public bool HasAccessibilityService => App.AccessibilityService is not null;

    /// <summary>Installs the APK.</summary>
    public AsyncRelayCommand InstallCommand { get; }

    /// <summary>Grants the declared permissions.</summary>
    public AsyncRelayCommand GrantCommand { get; }

    /// <summary>Updates the APK information.</summary>
    public void UpdateApk(ApkLocation? location)
    {
        Apk = location;
        ApkFound = location?.Found == true;
        ApkState = location is null
            ? "No apks folder was found next to FireReplace."
            : location.Found
                ? location.FileName!
                : $"Missing — expected a file matching {location.FileNamePattern}.";
    }

    /// <summary>Updates state from a device snapshot.</summary>
    public void Update(DeviceState state)
    {
        InstallState = state.StateOf(App.PackageName) switch
        {
            PackageState.Enabled => "Installed",
            PackageState.Disabled => "Installed but disabled",
            PackageState.NotInstalled => "Not installed",
            _ => "Unknown",
        };

        AccessibilityEnabled = App.AccessibilityService is not null
                               && state.HasAccessibilityService(App.AccessibilityService);
    }
}

/// <summary>
/// The launcher page: install Home on Fire and Projectivy, grant the permission Home on Fire needs,
/// merge accessibility services additively, and opt in to notification access.
/// </summary>
public sealed class LauncherViewModel : PageViewModel
{
    private string _accessibilityStatus = "Unknown";
    private bool _hasAccessibilityChanges;
    private bool _notificationEnabled;

    /// <summary>Creates the page.</summary>
    public LauncherViewModel(AppHost host)
        : base(host)
    {
        foreach (var app in LauncherCatalog.All)
        {
            Apps.Add(new LauncherAppViewModel(app, InstallAsync, GrantAsync));
        }

        ApplyAccessibilityCommand = new AsyncRelayCommand(ApplyAccessibilityAsync, () => State is not null);
        EnableNotificationsCommand = new AsyncRelayCommand(EnableNotificationsAsync, () => State is not null);
        OpenNotificationSettingsCommand = new AsyncRelayCommand(OpenNotificationSettingsAsync);
        RefreshCommand = new AsyncRelayCommand(ct => RefreshAsync(RefreshScope.All, ct));

        RefreshApks();
    }

    /// <inheritdoc />
    public override string Title => "Launcher";

    /// <inheritdoc />
    public override string Subtitle =>
        "Replace the Fire TV home screen. FireReplace only ever adds to the accessibility list, so the Home button redirect keeps working.";

    /// <summary>The launcher apps.</summary>
    public ObservableCollection<LauncherAppViewModel> Apps { get; } = [];

    /// <summary>Accessibility services currently enabled on the device.</summary>
    public ObservableCollection<string> CurrentAccessibility { get; } = [];

    /// <summary>The list FireReplace proposes writing.</summary>
    public ObservableCollection<string> ProposedAccessibility { get; } = [];

    /// <summary>Summary of the accessibility state.</summary>
    public string AccessibilityStatus
    {
        get => _accessibilityStatus;
        private set => SetProperty(ref _accessibilityStatus, value);
    }

    /// <summary>True when the proposed list differs from the current one.</summary>
    public bool HasAccessibilityChanges
    {
        get => _hasAccessibilityChanges;
        private set => SetProperty(ref _hasAccessibilityChanges, value);
    }

    /// <summary>True when Projectivy's notification listener is enabled.</summary>
    public bool NotificationEnabled
    {
        get => _notificationEnabled;
        private set => SetProperty(ref _notificationEnabled, value);
    }

    /// <summary>The notification listener component, for display.</summary>
    public string NotificationComponent => LauncherCatalog.Projectivy.NotificationListener ?? "—";

    /// <summary>Applies the additive accessibility merge.</summary>
    public AsyncRelayCommand ApplyAccessibilityCommand { get; }

    /// <summary>Grants notification access to Projectivy.</summary>
    public AsyncRelayCommand EnableNotificationsCommand { get; }

    /// <summary>Opens the notification listener settings screen on the TV.</summary>
    public AsyncRelayCommand OpenNotificationSettingsCommand { get; }

    /// <summary>Re-reads device state.</summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <inheritdoc />
    public override void OnDeviceStateChanged(DeviceState state)
    {
        base.OnDeviceStateChanged(state);

        foreach (var app in Apps)
        {
            app.Update(state);
        }

        var merge = Host.LauncherActions.PreviewAccessibilityMerge(state, LauncherCatalog.KnownAccessibilityServices);

        CurrentAccessibility.Clear();
        foreach (var component in merge.Current)
        {
            CurrentAccessibility.Add(Describe(component));
        }

        ProposedAccessibility.Clear();
        foreach (var component in merge.Proposed)
        {
            var added = merge.Added.Contains(component, StringComparer.Ordinal);
            ProposedAccessibility.Add(added ? $"{Describe(component)}   (added)" : Describe(component));
        }

        HasAccessibilityChanges = merge.HasChanges;
        AccessibilityStatus = merge.Current.Count == 0
            ? "No accessibility services are enabled."
            : merge.HasChanges
                ? $"{merge.Current.Count} enabled · {merge.Added.Count} would be added"
                : $"{merge.Current.Count} enabled · nothing to add";

        NotificationEnabled = LauncherCatalog.Projectivy.NotificationListener is { } listener
                              && state.HasNotificationListener(listener);

        ApplyAccessibilityCommand.RaiseCanExecuteChanged();
        EnableNotificationsCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Re-scans the APK folder and updates the rows.</summary>
    public void RefreshApks()
    {
        Host.RefreshToolchain();

        foreach (var app in Apps)
        {
            app.UpdateApk(Host.Apks.FirstOrDefault(a => a.PackageName == app.PackageName));
        }
    }

    private static string Describe(string component)
    {
        var friendly = LauncherCatalog.FriendlyComponentName(component);
        return string.Equals(friendly, component, StringComparison.Ordinal)
            ? component
            : $"{friendly}  —  {component}";
    }

    private async Task InstallAsync(LauncherAppViewModel row, CancellationToken cancellationToken)
    {
        if (State is not { } state || row.Apk is null || !row.Apk.Found)
        {
            await Dialogs.ShowInfoAsync(
                "APK not found",
                $"FireReplace could not find an APK for {row.DisplayName}.",
                $"Expected a file matching {row.App.ApkFilePattern} in {Host.ApkDirectory ?? "the apks folder"}.",
                MessageSeverity.Warning).ConfigureAwait(true);
            return;
        }

        var plan = Host.LauncherActions.BuildInstallPlan(row.App, row.Apk, state);
        await RunPlanAsync(plan, RefreshScope.All, cancellationToken).ConfigureAwait(true);
    }

    private async Task GrantAsync(LauncherAppViewModel row, CancellationToken cancellationToken)
    {
        if (State is null)
        {
            return;
        }

        var plan = Host.LauncherActions.BuildGrantPlan(row.App);
        await RunPlanAsync(plan, RefreshScope.None, cancellationToken).ConfigureAwait(true);
    }

    private async Task ApplyAccessibilityAsync(CancellationToken cancellationToken)
    {
        if (State is not { } state)
        {
            return;
        }

        var plan = Host.LauncherActions.BuildAccessibilityPlan(state, LauncherCatalog.KnownAccessibilityServices);

        if (plan.IsEmpty)
        {
            await Dialogs.ShowInfoAsync(
                "Nothing to change",
                "Both accessibility services are already enabled.",
                string.Join(System.Environment.NewLine, state.AccessibilityServices)).ConfigureAwait(true);
            return;
        }

        await RunPlanAsync(plan, RefreshScope.Settings, cancellationToken).ConfigureAwait(true);
    }

    private async Task EnableNotificationsAsync(CancellationToken cancellationToken)
    {
        if (State is not { } state)
        {
            return;
        }

        var plan = Host.LauncherActions.BuildNotificationPlan(LauncherCatalog.Projectivy, state);
        var report = await RunPlanAsync(plan, RefreshScope.Settings, cancellationToken).ConfigureAwait(true);

        if (report is null || report.WasDryRun || report.AllSucceeded)
        {
            return;
        }

        // Some Fire OS builds reject the command outright. Offer the manual route instead of failing.
        var choice = await Dialogs.ShowMessageAsync(new MessageDialogViewModel
        {
            Title = "Fire OS refused the notification command",
            Message = "FireReplace could not enable notification access over ADB.",
            Causes =
            [
                "Some Fire OS builds only allow this to be enabled from the TV itself.",
                "FireReplace can open the TV's notification-listener settings screen for you.",
                "Nothing else was changed.",
            ],
            Detail = string.Join(
                System.Environment.NewLine,
                report.Outcomes.Where(o => !o.Succeeded).Select(o => o.Failure?.RawDetail)),
            Severity = MessageSeverity.Warning,
            Buttons =
            [
                new MessageDialogButton("Open TV settings", "open", true),
                new MessageDialogButton("Close", "close"),
            ],
        }).ConfigureAwait(true);

        if (choice == "open")
        {
            await OpenNotificationSettingsAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task OpenNotificationSettingsAsync(CancellationToken cancellationToken)
    {
        var result = await Host.LauncherActions.OpenNotificationSettingsAsync(cancellationToken).ConfigureAwait(true);

        await Dialogs.ShowInfoAsync(
            result.Success ? "Look at your TV" : "Could not open the settings screen",
            result.Success
                ? "The notification-listener settings screen should now be open on the Fire TV. Enable Projectivy Launcher there."
                : "FireReplace could not open the settings screen on the TV.",
            result.Success ? null : result.CombinedOutput,
            result.Success ? MessageSeverity.Info : MessageSeverity.Error).ConfigureAwait(true);
    }
}
