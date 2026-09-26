namespace FireReplace.Core.Adb;

/// <summary>What happened when FireReplace tried to reach the device.</summary>
public enum ConnectionStatus
{
    /// <summary>Not connected and no attempt has been made.</summary>
    Idle,

    /// <summary>An attempt is in flight.</summary>
    Connecting,

    /// <summary>Connected and authorized.</summary>
    Connected,

    /// <summary>The device is reachable but has not authorized this computer.</summary>
    Unauthorized,

    /// <summary>The device could not be reached.</summary>
    Failed,

    /// <summary>adb.exe itself is missing or unusable.</summary>
    ToolchainMissing,
}

/// <summary>A connection result together with a human explanation and likely causes.</summary>
/// <param name="Status">Outcome.</param>
/// <param name="Headline">One-line, non-technical summary.</param>
/// <param name="Detail">Raw adb text, shown but never interpreted as a command.</param>
/// <param name="Causes">Things worth checking, in the order most likely to help.</param>
public sealed record ConnectionOutcome(
    ConnectionStatus Status,
    string Headline,
    string? Detail,
    IReadOnlyList<string> Causes)
{
    /// <summary>True when commands can be issued.</summary>
    public bool IsUsable => Status == ConnectionStatus.Connected;

    /// <summary>
    /// <c>adb connect</c> exits 0 even when it fails, so its text has to be inspected.
    /// </summary>
    public static bool IsFailureText(string? output) =>
        output is not null
        && (output.Contains("failed to connect", StringComparison.OrdinalIgnoreCase)
            || output.Contains("cannot connect", StringComparison.OrdinalIgnoreCase)
            || output.Contains("unable to connect", StringComparison.OrdinalIgnoreCase)
            || output.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
            || output.Contains("No route to host", StringComparison.OrdinalIgnoreCase));

    /// <summary>The standard set of things to check when a connection fails.</summary>
    public static readonly IReadOnlyList<string> ConnectionFailureCauses =
    [
        "The IP address does not match the Fire TV's current address.",
        "ADB debugging is switched off on the Fire TV.",
        "The PC and the Fire TV are on different networks or VLANs.",
        "The Fire TV has not authorized this PC yet.",
        "The Fire TV is asleep or powered off.",
        "A firewall is blocking TCP port 5555.",
    ];

    /// <summary>The standard set of things to check when the device reports <c>unauthorized</c>.</summary>
    public static readonly IReadOnlyList<string> AuthorizationCauses =
    [
        "Look at the TV screen: it may be asking whether to allow this computer to use ADB.",
        "Choose Allow (and optionally 'always allow from this computer').",
        "If no prompt appears, toggle ADB debugging off and on in Fire TV developer options.",
    ];

    /// <summary>Builds a successful outcome.</summary>
    public static ConnectionOutcome Connected(string serial) =>
        new(ConnectionStatus.Connected, $"Connected to {serial}", null, Array.Empty<string>());

    /// <summary>Builds an unauthorized outcome.</summary>
    public static ConnectionOutcome Unauthorized(string serial) =>
        new(
            ConnectionStatus.Unauthorized,
            "Check your TV",
            $"{serial} responded but has not authorized this computer. Your Fire TV may be asking whether to allow this computer to use ADB.",
            AuthorizationCauses);

    /// <summary>Builds a failure outcome.</summary>
    public static ConnectionOutcome Failed(string headline, string? detail) =>
        new(ConnectionStatus.Failed, headline, detail, ConnectionFailureCauses);

    /// <summary>Builds a toolchain-missing outcome.</summary>
    public static ConnectionOutcome ToolchainMissing(string detail) =>
        new(
            ConnectionStatus.ToolchainMissing,
            "ADB is not available",
            detail,
            [
                "Download Android SDK Platform Tools for Windows.",
                "Extract it so that platform-tools\\adb.exe sits next to FireReplace.exe.",
                "Or point Settings \u203A ADB \u203A Platform Tools location at an existing copy.",
            ]);
}
