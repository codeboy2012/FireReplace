namespace FireReplace.Core.Adb;

/// <summary>
/// The complete result of one external process invocation. Nothing is swallowed:
/// stdout, stderr and the exit code are always surfaced to the caller.
/// </summary>
/// <param name="ExitCode">Process exit code, or -1 when the process could not be started.</param>
/// <param name="StandardOutput">Captured stdout, trailing whitespace trimmed.</param>
/// <param name="StandardError">Captured stderr, trailing whitespace trimmed.</param>
/// <param name="TimedOut">True when the process was killed because it exceeded its timeout.</param>
/// <param name="Duration">Wall-clock duration of the invocation.</param>
public sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut,
    TimeSpan Duration)
{
    /// <summary>True when the process exited with code 0 and was not killed.</summary>
    public bool Success => ExitCode == 0 && !TimedOut;

    /// <summary>stderr when present, otherwise stdout. Useful for surfacing failures.</summary>
    public string BestErrorText =>
        !string.IsNullOrWhiteSpace(StandardError) ? StandardError.Trim() : StandardOutput.Trim();

    /// <summary>A synthetic result representing "the executable was missing".</summary>
    public static ProcessResult NotRun(string message) =>
        new(-1, string.Empty, message, false, TimeSpan.Zero);
}
