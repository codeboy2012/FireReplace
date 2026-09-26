using System.IO;
using System.Collections.ObjectModel;
using FireReplace.Core.Adb;
using FireReplace.Core.Catalog;
using FireReplace.Core.Services;
using FireReplace.Dialogs;
using FireReplace.Mvvm;
using FireReplace.Services;

namespace FireReplace.ViewModels;

/// <summary>One entry in the navigation sidebar.</summary>
public sealed class NavItemViewModel : ObservableObject
{
    private bool _isSelected;

    /// <summary>Creates the entry.</summary>
    /// <param name="label">Sidebar label.</param>
    /// <param name="factory">Creates the page on first use.</param>
    /// <param name="isSeparatedBelow">True to draw a divider above this entry.</param>
    internal NavItemViewModel(string label, Func<PageViewModel> factory, bool isSeparatedBelow = false)
    {
        Label = label;
        Factory = factory;
        HasSeparatorAbove = isSeparatedBelow;
    }

    /// <summary>Sidebar label.</summary>
    public string Label { get; }

    /// <summary>True to draw a divider above the entry.</summary>
    public bool HasSeparatorAbove { get; }

    /// <summary>Lazily created page instance.</summary>
    public PageViewModel? Page { get; private set; }

    private Func<PageViewModel> Factory { get; }

    /// <summary>Whether this entry is the active one.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Returns the page, creating it the first time.</summary>
    public PageViewModel Resolve() => Page ??= Factory();
}

/// <summary>
/// The root view model: window chrome state, navigation, connection status and the log panel.
/// </summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly AppHost _host;
    private object? _currentContent;
    private NavItemViewModel? _selectedNav;
    private WelcomeViewModel? _welcome;
    private bool _isConnected;
    private string _connectionLabel = "Not connected";
    private string _connectionDetail = "—";
    private ConnectionStatus _connectionStatus = ConnectionStatus.Idle;
    private bool _backupOffered;

    /// <summary>Creates the shell and builds the navigation list.</summary>
    public ShellViewModel(AppHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));

        Log = new LogViewModel(host.Log);

        Connect = new ConnectViewModel(host);
        Connect.Connected += async (_, _) => await OnConnectedAsync().ConfigureAwait(true);
        Connect.DiagnosticsRequested += (_, _) => NavigateTo("Diagnostics");

        NavItems =
        [
            new NavItemViewModel("Dashboard", () => new DashboardViewModel(host)),
            new NavItemViewModel("Launcher", () => new LauncherViewModel(host)),
            new NavItemViewModel("Performance", () => new SettingsGroupViewModel(
                host,
                "Performance",
                "Fire OS animates every transition. Turning the animation scales off makes the whole interface feel immediate.",
                [SettingsCatalog.Animations])),
            new NavItemViewModel("Privacy", () => new SettingsGroupViewModel(
                host,
                "Privacy",
                "Reduce on-device metrics upload and ad personalisation. These settings do not stop network traffic to Amazon: that needs router or DNS level blocking.",
                [SettingsCatalog.Privacy])),
            new NavItemViewModel("Debloat", () => new DebloatViewModel(host)),
            new NavItemViewModel("Apps", () => new AppsViewModel(host)),
            new NavItemViewModel("Diagnostics", () => new DiagnosticsViewModel(host)),
            new NavItemViewModel("Settings", CreatePreferences),
            new NavItemViewModel(
                "Advanced",
                () => new SettingsGroupViewModel(
                    host,
                    "Advanced / Experimental",
                    "Settings that were suggested during the original setup but never confirmed on the reference device. They are kept here so they are never part of a recommended action.",
                    SettingsCatalog.ExperimentalGroups,
                    "These settings were not part of the verified core configuration. They may not exist on your Fire OS build, and FireReplace cannot predict their effect."),
                isSeparatedBelow: true),
        ];

        NavigateCommand = new RelayCommand(parameter =>
        {
            if (parameter is NavItemViewModel item)
            {
                Select(item);
            }
        });

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => IsConnected);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, () => IsConnected);
        ToggleDryRunCommand = new RelayCommand(ToggleDryRun);

        host.Connection.StatusChanged += (_, outcome) => ApplyConnectionOutcome(outcome);
        host.DeviceStates.StateChanged += (_, state) => PublishState(state);

        _currentContent = Connect;
    }

    /// <summary>The live log panel.</summary>
    public LogViewModel Log { get; }

    /// <summary>The setup/connection page, shown until a device is connected.</summary>
    public ConnectViewModel Connect { get; }

    /// <summary>Overlay dialog host.</summary>
    public DialogService Dialogs => _host.Dialogs;

    /// <summary>Sidebar entries.</summary>
    public ObservableCollection<NavItemViewModel> NavItems { get; }

    /// <summary>Content currently displayed in the main area.</summary>
    public object? CurrentContent
    {
        get => _currentContent;
        private set => SetProperty(ref _currentContent, value);
    }

    /// <summary>The first-run overlay, or null when it is not needed.</summary>
    public WelcomeViewModel? Welcome
    {
        get => _welcome;
        private set
        {
            if (SetProperty(ref _welcome, value))
            {
                OnPropertyChanged(nameof(IsWelcomeVisible));
            }
        }
    }

    /// <summary>True while the first-run overlay is shown.</summary>
    public bool IsWelcomeVisible => _welcome is not null;

    /// <summary>True once a device is connected and authorized.</summary>
    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                DisconnectCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Connection status for the indicator.</summary>
    public ConnectionStatus ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetProperty(ref _connectionStatus, value);
    }

    /// <summary>Status line shown at the bottom of the sidebar.</summary>
    public string ConnectionLabel
    {
        get => _connectionLabel;
        private set => SetProperty(ref _connectionLabel, value);
    }

    /// <summary>Serial or address shown under the status line.</summary>
    public string ConnectionDetail
    {
        get => _connectionDetail;
        private set => SetProperty(ref _connectionDetail, value);
    }

    /// <summary>True when dry run is on, so the shell can show the banner.</summary>
    public bool IsDryRun => _host.Adb.DryRun;

    /// <summary>Window title.</summary>
    public string WindowTitle => "FireReplace";

    /// <summary>Navigates to the entry passed as a command parameter.</summary>
    public RelayCommand NavigateCommand { get; }

    /// <summary>Re-reads the full device snapshot.</summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>Disconnects and returns to the connection screen.</summary>
    public AsyncRelayCommand DisconnectCommand { get; }

    /// <summary>Turns dry run on or off from the header.</summary>
    public RelayCommand ToggleDryRunCommand { get; }

    /// <summary>
    /// Runs the deferred startup work: the first-run checklist and, when configured, an automatic
    /// connection attempt. Nothing here blocks the first frame.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (!_host.Config.FirstRunCompleted)
        {
            ShowWelcome();
            return;
        }

        await AutoConnectAsync().ConfigureAwait(true);
    }

    /// <summary>Shows the first-run checklist overlay.</summary>
    public void ShowWelcome()
    {
        var welcome = new WelcomeViewModel();
        welcome.Completed += async (_, _) =>
        {
            _host.Config.FirstRunCompleted = true;
            _host.SaveConfig();
            Welcome = null;
            await AutoConnectAsync().ConfigureAwait(true);
        };

        Welcome = welcome;
    }

    /// <summary>Navigates to the sidebar entry with the given label.</summary>
    public void NavigateTo(string label)
    {
        var item = NavItems.FirstOrDefault(n => string.Equals(n.Label, label, StringComparison.Ordinal));
        if (item is not null)
        {
            Select(item);
        }
    }

    /// <summary>Persists anything worth keeping between runs.</summary>
    public void PersistState()
    {
        if (_host.Config.RememberLastIp && !string.IsNullOrWhiteSpace(Connect.IpAddress))
        {
            _host.Config.LastIpAddress = Connect.IpAddress.Trim();
        }
    }

    private PreferencesViewModel CreatePreferences()
    {
        var page = new PreferencesViewModel(_host);
        page.FirstRunRequested += (_, _) => ShowWelcome();
        return page;
    }

    private async Task AutoConnectAsync()
    {
        if (!_host.Config.AutoConnectOnStartup
            || string.IsNullOrWhiteSpace(_host.Config.LastIpAddress)
            || !_host.AdbLocation.Found)
        {
            return;
        }

        await Connect.ConnectCommand.ExecuteAsync().ConfigureAwait(true);
    }

    private void Select(NavItemViewModel item)
    {
        if (!IsConnected)
        {
            return;
        }

        foreach (var nav in NavItems)
        {
            nav.IsSelected = ReferenceEquals(nav, item);
        }

        _selectedNav = item;
        var page = item.Resolve();

        if (_host.DeviceStates.Current is { } state)
        {
            page.OnDeviceStateChanged(state);
        }

        CurrentContent = page;
        _ = page.ActivateAsync();
    }

    private async Task OnConnectedAsync()
    {
        IsConnected = true;

        await _host.DeviceStates.RefreshAsync().ConfigureAwait(true);

        if (_selectedNav is null)
        {
            NavigateTo("Dashboard");
        }
        else
        {
            Select(_selectedNav);
        }

        await OfferBackupAsync().ConfigureAwait(true);
    }

    private async Task OfferBackupAsync()
    {
        if (_backupOffered
            || !_host.Config.OfferBackupBeforeChanges
            || _host.DeviceStates.Current is not { } state)
        {
            return;
        }

        _backupOffered = true;

        var choice = await _host.Dialogs.ShowMessageAsync(new MessageDialogViewModel
        {
            Title = "Save a configuration backup?",
            Message = "FireReplace can record the settings and package states it is able to change, so you can put them back later.",
            Causes =
            [
                "The backup holds setting values, package enable/disable states and device identity.",
                "It does not touch your apps, accounts or any private data.",
                $"Files are written to {_host.Paths.BackupDirectory}.",
            ],
            Severity = MessageSeverity.Info,
            Buttons =
            [
                new MessageDialogButton("Create backup", "backup", true),
                new MessageDialogButton("Not now", "skip"),
            ],
        }).ConfigureAwait(true);

        if (choice != "backup")
        {
            return;
        }

        try
        {
            var entry = await _host.Backups.CreateAsync(state).ConfigureAwait(true);
            await _host.Dialogs.ShowInfoAsync(
                "Backup created",
                $"Saved {entry.SettingCount} setting value(s) and {entry.PackageCount} package state(s).",
                entry.Directory,
                MessageSeverity.Success).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _host.Dialogs.ShowInfoAsync(
                "Backup failed",
                "FireReplace could not write the backup folder.",
                ex.Message,
                MessageSeverity.Error).ConfigureAwait(true);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            return;
        }

        var outcome = await _host.Connection.VerifyAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
        if (!outcome.IsUsable)
        {
            ApplyConnectionOutcome(outcome);
            return;
        }

        await _host.DeviceStates.RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await _host.Connection.DisconnectAsync(cancellationToken).ConfigureAwait(true);
        _host.DeviceStates.Reset();

        IsConnected = false;
        foreach (var nav in NavItems)
        {
            nav.IsSelected = false;
        }

        _selectedNav = null;
        _backupOffered = false;
        Connect.RefreshToolchain();
        CurrentContent = Connect;
    }

    private void ApplyConnectionOutcome(ConnectionOutcome outcome)
    {
        ConnectionStatus = outcome.Status;

        switch (outcome.Status)
        {
            case ConnectionStatus.Connected:
                ConnectionLabel = "Connected";
                ConnectionDetail = _host.Connection.Serial ?? "—";
                IsConnected = true;
                break;

            case ConnectionStatus.Connecting:
                ConnectionLabel = "Connecting";
                ConnectionDetail = _host.Connection.IpAddress ?? "—";
                break;

            case ConnectionStatus.Unauthorized:
                ConnectionLabel = "Waiting for TV";
                ConnectionDetail = _host.Connection.Serial ?? "—";
                IsConnected = false;
                CurrentContent = Connect;
                break;

            default:
                ConnectionLabel = "Not connected";
                ConnectionDetail = "—";
                IsConnected = false;
                CurrentContent = Connect;
                break;
        }
    }

    private void PublishState(DeviceState state)
    {
        foreach (var nav in NavItems)
        {
            nav.Page?.OnDeviceStateChanged(state);
        }
    }

    private void ToggleDryRun()
    {
        _host.Config.DryRun = !_host.Config.DryRun;
        _host.Adb.DryRun = _host.Config.DryRun;
        _host.SaveConfig();
        OnPropertyChanged(nameof(IsDryRun));

        if (_host.DeviceStates.Current is { } state)
        {
            PublishState(state);
        }
    }
}
