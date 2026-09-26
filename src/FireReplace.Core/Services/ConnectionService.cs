using FireReplace.Core.Adb;
using FireReplace.Core.Logging;
using FireReplace.Core.Models;
using FireReplace.Core.Validation;

namespace FireReplace.Core.Services;

/// <summary>
/// Owns the connection lifecycle: connect, verify, detect the authorization prompt, disconnect.
/// </summary>
/// <remarks>
/// ADB authorization is never bypassed. When the device reports <c>unauthorized</c> the user is told
/// to look at the TV and approve the prompt; FireReplace only offers Retry and Disconnect.
/// </remarks>
public sealed class ConnectionService
{
    private readonly IAdbService _adb;
    private readonly ILogService _log;

    /// <summary>Creates the service.</summary>
    public ConnectionService(IAdbService adb, ILogService log)
    {
        _adb = adb ?? throw new ArgumentNullException(nameof(adb));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Current connection status.</summary>
    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Idle;

    /// <summary>Serial of the connected device, when connected.</summary>
    public string? Serial { get; private set; }

    /// <summary>IP address last used for a connection attempt.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Raised whenever <see cref="Status"/> changes.</summary>
    public event EventHandler<ConnectionOutcome>? StatusChanged;

    /// <summary>
    /// Connects to <paramref name="ipAddress"/> and verifies the result with <c>adb devices</c>.
    /// </summary>
    public async Task<ConnectionOutcome> ConnectAsync(
        string ipAddress,
        int port = 5555,
        CancellationToken cancellationToken = default)
    {
        if (!Validate.IsIpAddress(ipAddress))
        {
            return Publish(new ConnectionOutcome(
                ConnectionStatus.Failed,
                "That does not look like an IP address.",
                $"'{ipAddress}' is not a valid IPv4 or IPv6 address.",
                ["Find the address under Settings \u203A My Fire TV \u203A About \u203A Network on the TV.", "It usually looks like 192.168.1.147."]));
        }

        if (string.IsNullOrWhiteSpace(_adb.AdbPath))
        {
            return Publish(ConnectionOutcome.ToolchainMissing(
                "adb.exe was not found. FireReplace looks for platform-tools\\adb.exe next to the application."));
        }

        IpAddress = ipAddress.Trim();
        var endpoint = AdbArguments.FormatEndpoint(IpAddress, port);

        Status = ConnectionStatus.Connecting;
        _log.Write(LogLevel.Info, "connect", $"Connecting to {endpoint}");

        var connect = await _adb.ConnectAsync(IpAddress, port, cancellationToken).ConfigureAwait(false);

        if (!connect.Success)
        {
            var failure = AdbErrorInterpreter.Interpret(connect, $"connect to {endpoint}");
            return Publish(new ConnectionOutcome(ConnectionStatus.Failed, failure.Headline, failure.RawDetail, failure.Causes));
        }

        if (ConnectionOutcome.IsFailureText(connect.CombinedOutput))
        {
            return Publish(ConnectionOutcome.Failed("ADB could not connect to the Fire TV.", connect.CombinedOutput));
        }

        return Publish(await VerifyAsync(endpoint, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Re-checks the current connection with <c>adb devices</c>. Used by Retry after an
    /// authorization prompt and by the diagnostics page.
    /// </summary>
    public async Task<ConnectionOutcome> VerifyAsync(string? endpoint = null, CancellationToken cancellationToken = default)
    {
        var target = endpoint ?? Serial ?? _adb.Serial;

        if (string.IsNullOrWhiteSpace(target))
        {
            return Publish(new ConnectionOutcome(ConnectionStatus.Idle, "Not connected", null, Array.Empty<string>()));
        }

        var devices = await _adb.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
        var entry = devices.FirstOrDefault(d => string.Equals(d.Serial, target, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return Publish(ConnectionOutcome.Failed(
                "The Fire TV did not appear in the device list.",
                devices.Count == 0
                    ? "adb devices returned no devices."
                    : "adb devices returned: " + string.Join(", ", devices.Select(d => $"{d.Serial} ({d.RawState})"))));
        }

        switch (entry.State)
        {
            case AdbDeviceState.Online:
                Serial = entry.Serial;
                _adb.Serial = entry.Serial;
                _log.Write(LogLevel.Success, "connect", $"Connected to {entry.Serial}");
                return ConnectionOutcome.Connected(entry.Serial);

            case AdbDeviceState.Unauthorized:
            case AdbDeviceState.Authorizing:
                Serial = entry.Serial;
                _adb.Serial = entry.Serial;
                _log.Write(LogLevel.Warning, "connect", $"{entry.Serial} is not authorized yet — check the TV.");
                return ConnectionOutcome.Unauthorized(entry.Serial);

            default:
                return ConnectionOutcome.Failed(
                    $"The Fire TV reported state '{entry.RawState}'.",
                    "A device in this state cannot accept commands yet.");
        }
    }

    /// <summary>Disconnects and clears the cached serial.</summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (Serial is not null)
        {
            await _adb.DisconnectAsync(Serial, cancellationToken).ConfigureAwait(false);
            _log.Write(LogLevel.Info, "connect", $"Disconnected from {Serial}");
        }

        Serial = null;
        _adb.Serial = null;
        Publish(new ConnectionOutcome(ConnectionStatus.Idle, "Not connected", null, Array.Empty<string>()));
    }

    private ConnectionOutcome Publish(ConnectionOutcome outcome)
    {
        Status = outcome.Status;
        if (outcome.Status is ConnectionStatus.Failed or ConnectionStatus.Idle or ConnectionStatus.ToolchainMissing)
        {
            Serial = outcome.Status == ConnectionStatus.Failed ? Serial : null;
        }

        StatusChanged?.Invoke(this, outcome);
        return outcome;
    }
}
