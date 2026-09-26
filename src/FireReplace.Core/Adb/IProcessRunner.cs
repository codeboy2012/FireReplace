namespace FireReplace.Core.Adb;

/// <summary>
/// Abstraction over launching an external process with an explicit argument list.
/// Implementations must never build a command line by string concatenation and must
/// never route through a shell interpreter.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="executablePath"/> with the supplied arguments and captures its output.
    /// </summary>
    /// <param name="executablePath">Absolute path to the executable.</param>
    /// <param name="arguments">Arguments passed individually; escaping is handled by the runtime.</param>
    /// <param name="timeout">Maximum wall-clock time before the process is killed.</param>
    /// <param name="cancellationToken">Cancels the wait and kills the process tree.</param>
    Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
