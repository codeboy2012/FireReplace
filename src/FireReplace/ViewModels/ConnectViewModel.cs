using System.Collections.ObjectModel;
using FireReplace.Core.Adb;
using FireReplace.Dialogs;
using FireReplace.Core.Discovery;
using FireReplace.Core.Validation;
using FireReplace.Mvvm;
using FireReplace.Services;

namespace FireReplace.ViewModels;

/// <summary>One line in the setup checklist.</summary>
public sealed class SetupItemViewModel : ObservableObject
{
    private bool _isSatisfied;
    private string _detail = string.Empty;

    /// <summary>Label, for example "adb.exe".</summary>
    public required string Label { get; init; }

    /// <summary>True when the requirement is met.</summary>
    public bool IsSatisfied
    {
        get => _isSatisfied;
        set
        {
            if (SetProperty(ref _isSatisfied, value))
            {
                OnPropertyChanged(nameof(Glyph));
            }
        }
    }

    /// <summary>Path or explanation shown next to the label.</summary>
    public string Detail
    {
        get => _detail;
        set => SetProperty(ref _detail, value);
    }

    /// <summary>Tick or cross.</summary>
    public string Glyph => IsSatisfied ? "\u2713" : "\u2717";
}

/// <summary>
/// The setup and connection screen. It appears first, is deliberately tiny, and does no device
/// communication until the user presses Connect.
/// </summary>
public sealed class ConnectViewModel : PageViewModel
{
    private string _ipAddress = string.Empty;
    private string _statusHeadline = "Not connected";
    private string? _statusDetail;
    private ConnectionStatus _status = ConnectionStatus.Idle;
    private bool _showAuthorizationHelp;

    /// <summary>Creates the view model.</summary>
    public ConnectViewModel(AppHost host)
        : base(host)
    {
        _ipAddress = host.Config.RememberLastIp ? host.Config.LastIpAddress ?? string.Empty : string.Empty;

        ConnectCommand = new AsyncRelayCommand(
            ConnectAsync,
            () => Validate.IsIpAddress(_ipAddress) && Host.AdbLocation.Found);

        RetryCommand = new AsyncRelayCommand(RetryAsync);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync);
        RefreshToolchainCommand = new RelayCommand(RefreshToolchain);

        RefreshToolchain();
    }

    /// <summary>Raised when a connection has been established and verified.</summary>
    public event EventHandler? Connected;

    /// <summary>Raised when the user asks to open the diagnostics page from an error.</summary>
    public event EventHandler? DiagnosticsRequested;

    /// <inheritdoc />
    public override string Title => "FireReplace";

    /// <inheritdoc />
    public override string Subtitle => "Fire TV customization made simple.";

    /// <summary>The IP address entered by the user.</summary>
    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            if (SetProperty(ref _ipAddress, value))
            {
                OnPropertyChanged(nameof(IsIpValid));
                ConnectCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>True when the entered text is a usable IP address.</summary>
    public bool IsIpValid => Validate.IsIpAddress(_ipAddress);

    /// <summary>Current connection status.</summary>
    public ConnectionStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertiesChanged(nameof(IsConnected), nameof(IsUnauthorized), nameof(HasError));
            }
        }
    }

    /// <summary>Short status line.</summary>
    public string StatusHeadline
    {
        get => _statusHeadline;
        private set => SetProperty(ref _statusHeadline, value);
    }

    /// <summary>Detail text under the status line.</summary>
    public string? StatusDetail
    {
        get => _statusDetail;
        private set => SetProperty(ref _statusDetail, value);
    }

    /// <summary>Things to check, populated on failure.</summary>
    public ObservableCollection<string> Causes { get; } = [];

    /// <summary>Setup checklist rows.</summary>
    public ObservableCollection<SetupItemViewModel> PlatformToolsItems { get; } = [];

    /// <summary>APK checklist rows.</summary>
    public ObservableCollection<SetupItemViewModel> ApkItems { get; } = [];

    /// <summary>True when the device is connected and authorized.</summary>
    public bool IsConnected => Status == ConnectionStatus.Connected;

    /// <summary>True when the TV has not authorized this computer.</summary>
    public bool IsUnauthorized => Status == ConnectionStatus.Unauthorized;

    /// <summary>True when the last attempt failed.</summary>
    public bool HasError => Status is ConnectionStatus.Failed or ConnectionStatus.ToolchainMissing;

    /// <summary>True when the authorization explanation should be shown.</summary>
    public bool ShowAuthorizationHelp
    {
        get => _showAuthorizationHelp;
        private set => SetProperty(ref _showAuthorizationHelp, value);
    }

    /// <summary>True when adb.exe is available.</summary>
    public bool IsToolchainReady => Host.AdbLocation.Complete;

    /// <summary>Starts a connection attempt.</summary>
    public AsyncRelayCommand ConnectCommand { get; }

    /// <summary>Re-checks the current connection, used after approving the TV prompt.</summary>
    public AsyncRelayCommand RetryCommand { get; }

    /// <summary>Disconnects.</summary>
    public AsyncRelayCommand DisconnectCommand { get; }

    /// <summary>Re-runs adb/APK discovery.</summary>
    public RelayCommand RefreshToolchainCommand { get; }

    /// <summary>Re-runs discovery and rebuilds the checklist.</summary>
    public void RefreshToolchain()
    {
        Host.RefreshToolchain();

        PlatformToolsItems.Clear();
        var location = Host.AdbLocation;

        PlatformToolsItems.Add(new SetupItemViewModel
        {
            Label = "adb.exe",
            IsSatisfied = location.Found,
            Detail = location.Found
                ? location.AdbPath!
                : "Not found. Put Android SDK Platform Tools in a platform-tools folder next to FireReplace.exe.",
        });

        foreach (var missing in location.MissingSupportLibraries)
        {
            PlatformToolsItems.Add(new SetupItemViewModel
            {
                Label = missing,
                IsSatisfied = false,
                Detail = "Windows needs this DLL next to adb.exe. Copy the whole platform-tools folder, not just adb.exe.",
            });
        }

        if (location.Found && location.MissingSupportLibraries.Count == 0)
        {
            PlatformToolsItems.Add(new SetupItemViewModel
            {
                Label = "Windows USB libraries",
                IsSatisfied = true,
                Detail = "AdbWinApi.dll and AdbWinUsbApi.dll are present.",
            });
        }

        ApkItems.Clear();
        foreach (var apk in Host.Apks)
        {
            ApkItems.Add(new SetupItemViewModel
            {
                Label = apk.DisplayName,
                IsSatisfied = apk.Found,
                Detail = apk.Found
                    ? apk.FileName!
                    : $"No file matching {apk.FileNamePattern} in {Host.ApkDirectory ?? "the apks folder"}.",
            });
        }

        if (Host.Apks.Count == 0)
        {
            ApkItems.Add(new SetupItemViewModel
            {
                Label = "APK folder",
                IsSatisfied = false,
                Detail = "No apks folder was found next to FireReplace.exe.",
            });
        }

        OnPropertiesChanged(nameof(IsToolchainReady));
        ConnectCommand.RaiseCanExecuteChanged();
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (!Host.AdbLocation.Found)
        {
            Apply(ConnectionOutcome.ToolchainMissing("adb.exe was not found next to FireReplace."));
            return;
        }

        Status = ConnectionStatus.Connecting;
        StatusHeadline = "Connecting…";
        StatusDetail = null;
        Causes.Clear();

        var outcome = await Host.Connection
            .ConnectAsync(IpAddress, Host.Config.AdbPort, cancellationToken)
            .ConfigureAwait(true);

        Apply(outcome);

        if (outcome.IsUsable)
        {
            Host.Config.LastIpAddress = IpAddress.Trim();
            Host.SaveConfig();
            Connected?.Invoke(this, EventArgs.Empty);
        }
        else if (outcome.Status == ConnectionStatus.Failed)
        {
            var choice = await Dialogs.ShowMessageAsync(new MessageDialogViewModel
            {
                Title = "Could not connect",
                Message = outcome.Headline,
                Causes = outcome.Causes,
                Detail = outcome.Detail,
                Severity = MessageSeverity.Error,
                Buttons =
                [
                    new MessageDialogButton("Retry", "retry", true),
                    new MessageDialogButton("Back", "back"),
                    new MessageDialogButton("Diagnostics", "diagnostics"),
                ],
            }).ConfigureAwait(true);

            switch (choice)
            {
                case "retry":
                    await ConnectAsync(cancellationToken).ConfigureAwait(true);
                    break;
                case "diagnostics":
                    DiagnosticsRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
    }

    private async Task RetryAsync(CancellationToken cancellationToken)
    {
        if (Host.Connection.Serial is null)
        {
            await ConnectAsync(cancellationToken).ConfigureAwait(true);
            return;
        }

        var outcome = await Host.Connection.VerifyAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
        Apply(outcome);

        if (outcome.IsUsable)
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await Host.Connection.DisconnectAsync(cancellationToken).ConfigureAwait(true);
        Host.DeviceStates.Reset();
        Apply(new ConnectionOutcome(ConnectionStatus.Idle, "Not connected", null, Array.Empty<string>()));
    }

    private void Apply(ConnectionOutcome outcome)
    {
        Status = outcome.Status;
        StatusHeadline = outcome.Status == ConnectionStatus.Connected
            ? "CONNECTED"
            : outcome.Headline;
        StatusDetail = outcome.Status == ConnectionStatus.Connected
            ? Host.Connection.Serial
            : outcome.Detail;

        ShowAuthorizationHelp = outcome.Status == ConnectionStatus.Unauthorized;

        Causes.Clear();
        foreach (var cause in outcome.Causes)
        {
            Causes.Add(cause);
        }
    }
}
