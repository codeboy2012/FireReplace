namespace FireReplace.Core.Models;

/// <summary>State reported for a device by <c>adb devices</c>.</summary>
public enum AdbDeviceState
{
    /// <summary>Reported state was not recognised.</summary>
    Unknown = 0,

    /// <summary><c>device</c> — connected and authorized.</summary>
    Online,

    /// <summary><c>unauthorized</c> — the device has not accepted this computer's ADB key.</summary>
    Unauthorized,

    /// <summary><c>offline</c> — reachable but not usable.</summary>
    Offline,

    /// <summary><c>authorizing</c> — the handshake is in progress.</summary>
    Authorizing,

    /// <summary><c>no permissions</c> / other transient connection states.</summary>
    Connecting,
}

/// <summary>One line of parsed <c>adb devices</c> output.</summary>
/// <param name="Serial">Device serial, for example <c>192.168.1.147:5555</c>.</param>
/// <param name="State">Parsed connection state.</param>
/// <param name="RawState">The exact text reported by adb, kept for the log and diagnostics.</param>
public sealed record AdbDeviceEntry(string Serial, AdbDeviceState State, string RawState)
{
    /// <summary>True when this device can accept shell commands.</summary>
    public bool IsUsable => State == AdbDeviceState.Online;
}
