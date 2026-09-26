namespace FireReplace.Core.Adb;

/// <summary>
/// The outcome of one ADB invocation, including the exact argument list that was used so the UI
/// and log can show precisely what ran.
/// </summary>
/// <param name="Arguments">Arguments passed to adb, excluding the executable itself.</param>
/// <param name="Process">Captured process result.</param>
/// <param name="WasDryRun">True when the command was displayed but deliberately not executed.</param>
public sealed record AdbCommandResult(
    IReadOnlyList<string> Arguments,
    ProcessResult Process,
    bool WasDryRun)
{
    /// <summary>True when adb exited cleanly, or when the call was skipped by dry run.</summary>
    public bool Success => WasDryRun || Process.Success;

    /// <summary>Trimmed stdout.</summary>
    public string Output => Process.StandardOutput;

    /// <summary>Trimmed stderr.</summary>
    public string Error => Process.StandardError;

    /// <summary>stdout and stderr combined, for text inspection of tools like <c>pm</c>.</summary>
    public string CombinedOutput =>
        string.IsNullOrWhiteSpace(Error) ? Output : $"{Output}{System.Environment.NewLine}{Error}".Trim();

    /// <summary>Process exit code, or -1 when adb never ran.</summary>
    public int ExitCode => Process.ExitCode;

    /// <summary>True when the command was killed for exceeding its timeout.</summary>
    public bool TimedOut => Process.TimedOut;

    /// <summary>The command rendered for display, for example <c>adb -s 192.168.1.5:5555 shell pm enable x</c>.</summary>
    public string DisplayCommand => Format(Arguments);

    /// <summary>Renders an argument list the way it would appear on a command line.</summary>
    public static string Format(IReadOnlyList<string> arguments)
    {
        var parts = arguments.Select(a => a.Contains(' ', StringComparison.Ordinal) ? $"\"{a}\"" : a);
        return "adb " + string.Join(' ', parts);
    }

    /// <summary>Creates a synthetic dry-run result for the given arguments.</summary>
    public static AdbCommandResult DryRun(IReadOnlyList<string> arguments) =>
        new(arguments, new ProcessResult(0, string.Empty, string.Empty, false, TimeSpan.Zero), true);
}
