using System.Diagnostics;
using System.Text;

namespace FireReplace.Core.Adb;

/// <summary>
/// Default <see cref="IProcessRunner"/> built on <see cref="Process"/> with
/// <see cref="ProcessStartInfo.ArgumentList"/>. No shell is involved at any point.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        if (!File.Exists(executablePath))
        {
            return ProcessResult.NotRun($"Executable not found: {executablePath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var stopwatch = Stopwatch.StartNew();
        using var process = new Process { StartInfo = startInfo };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return ProcessResult.NotRun($"Could not start {Path.GetFileName(executablePath)}: {ex.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            timedOut = timeoutSource.IsCancellationRequested;
            TryKill(process);

            if (!timedOut)
            {
                // Genuine caller cancellation: let it propagate so the UI can abandon the operation.
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        // Flush the async readers. WaitForExit() with no argument waits for redirected streams to close.
        try
        {
            process.WaitForExit();
        }
        catch (InvalidOperationException)
        {
            // Process was already released; the captured buffers are still valid.
        }

        stopwatch.Stop();

        var exitCode = timedOut ? -1 : SafeExitCode(process);
        var errorText = stderr.ToString().TrimEnd();
        if (timedOut)
        {
            errorText = string.IsNullOrEmpty(errorText)
                ? $"The command did not finish within {timeout.TotalSeconds:0.#}s and was cancelled."
                : errorText;
        }

        return new ProcessResult(exitCode, stdout.ToString().TrimEnd(), errorText, timedOut, stopwatch.Elapsed);
    }

    private static int SafeExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            // Nothing useful to do: the process is gone or cannot be killed.
        }
    }
}
