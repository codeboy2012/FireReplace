namespace FireReplace.Core.Adb;

/// <summary>A failure translated into something a person can act on.</summary>
/// <param name="Headline">Plain-language description of what went wrong.</param>
/// <param name="Causes">Likely causes, most useful first.</param>
/// <param name="RawDetail">The untouched adb text, always shown so nothing is hidden.</param>
/// <param name="IsProtectedByFireOs">True when Fire OS refused the operation on purpose.</param>
public sealed record AdbFailure(
    string Headline,
    IReadOnlyList<string> Causes,
    string? RawDetail,
    bool IsProtectedByFireOs = false);

/// <summary>
/// Turns adb exit codes and stderr text into readable explanations. "Exit code 1" is never the
/// message a user sees, but the raw text is always still available underneath.
/// </summary>
public static class AdbErrorInterpreter
{
    /// <summary>Interprets a completed command.</summary>
    /// <param name="result">The command result.</param>
    /// <param name="operation">Short description of what was attempted, for example "disable com.example".</param>
    public static AdbFailure Interpret(AdbCommandResult result, string operation)
    {
        ArgumentNullException.ThrowIfNull(result);

        var text = result.CombinedOutput;

        if (result.TimedOut)
        {
            return new AdbFailure(
                $"The Fire TV stopped responding while FireReplace tried to {operation}.",
                [
                    "The TV may have gone to sleep or dropped off the network.",
                    "Reconnect and try again.",
                    "Increase the connection timeout in Settings if your network is slow.",
                ],
                text);
        }

        if (result.ExitCode == -1 && text.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return new AdbFailure(
                "FireReplace could not start adb.exe.",
                [
                    "platform-tools\\adb.exe is missing or was moved.",
                    "AdbWinApi.dll must sit next to adb.exe on Windows.",
                    "Check Settings \u203A ADB \u203A Platform Tools location.",
                ],
                text);
        }

        if (AdbOutputParser.IndicatesSecurityException(text))
        {
            return new AdbFailure(
                "Fire OS protected this package.",
                [
                    "FireReplace will not attempt to bypass that protection.",
                    "This is a platform restriction, not a FireReplace limitation.",
                    "Everything else you selected was unaffected.",
                ],
                text,
                IsProtectedByFireOs: true);
        }

        if (text.Contains("device unauthorized", StringComparison.OrdinalIgnoreCase)
            || text.Contains("device still authorizing", StringComparison.OrdinalIgnoreCase))
        {
            return new AdbFailure(
                "The Fire TV has not authorized this computer.",
                ConnectionOutcome.AuthorizationCauses,
                text);
        }

        if (text.Contains("device offline", StringComparison.OrdinalIgnoreCase)
            || text.Contains("device '", StringComparison.OrdinalIgnoreCase) && text.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || text.Contains("no devices/emulators found", StringComparison.OrdinalIgnoreCase))
        {
            return new AdbFailure(
                "The Fire TV is no longer connected.",
                [
                    "The TV went to sleep or left the network.",
                    "Reconnect from the connection screen.",
                    "Confirm the IP address has not changed (DHCP leases move).",
                ],
                text);
        }

        if (ConnectionOutcome.IsFailureText(text))
        {
            return new AdbFailure(
                "ADB could not connect to the Fire TV.",
                ConnectionOutcome.ConnectionFailureCauses,
                text);
        }

        if (text.Contains("Unknown package", StringComparison.OrdinalIgnoreCase))
        {
            return new AdbFailure(
                "That package is not installed on this Fire TV.",
                [
                    "Package names differ between Fire OS builds and models.",
                    "Nothing was changed on the device.",
                ],
                text);
        }

        var installFailure = AdbOutputParser.ExtractInstallFailure(text);
        if (installFailure is not null)
        {
            return new AdbFailure(
                $"The install was rejected by the Fire TV ({installFailure}).",
                InstallCausesFor(installFailure),
                text);
        }

        return new AdbFailure(
            $"FireReplace could not {operation}.",
            [
                "The exact adb output is shown below and in the log.",
                "Retrying often helps when the TV was briefly busy.",
            ],
            string.IsNullOrWhiteSpace(text) ? $"adb exited with code {result.ExitCode}." : text);
    }

    private static IReadOnlyList<string> InstallCausesFor(string failureCode)
    {
        if (failureCode.Contains("NO_MATCHING_ABIS", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "The APK does not include a build for this device's CPU architecture.",
                "Check the device ABI on the Dashboard and download a matching APK.",
            ];
        }

        if (failureCode.Contains("VERSION_DOWNGRADE", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "A newer version of this app is already installed.",
                "Uninstall the newer version on the TV first, or use a newer APK.",
            ];
        }

        if (failureCode.Contains("INSUFFICIENT_STORAGE", StringComparison.OrdinalIgnoreCase))
        {
            return ["The Fire TV does not have enough free storage.", "Remove an unused app and try again."];
        }

        if (failureCode.Contains("UPDATE_INCOMPATIBLE", StringComparison.OrdinalIgnoreCase)
            || failureCode.Contains("INCONSISTENT_CERTIFICATES", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "An existing install of this package was signed with a different key.",
                "Uninstall the existing app on the TV, then install again.",
            ];
        }

        return
        [
            "The package manager rejected the APK.",
            "The full failure code is shown below.",
        ];
    }
}
