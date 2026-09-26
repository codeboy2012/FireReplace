using System.IO;
using System.Text;

namespace FireReplace.Services;

/// <summary>
/// Last-resort crash logging. Registered before anything else so a failure during resource loading
/// still leaves a readable trace instead of a bare exit code.
/// </summary>
public static class CrashReporter
{
    private static readonly object Gate = new();
    private static bool _installed;

    /// <summary>Path of the crash log.</summary>
    public static string LogPath { get; } = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
        "FireReplace",
        "crash.log");

    /// <summary>Hooks the process-wide unhandled exception events. Safe to call more than once.</summary>
    public static void Install()
    {
        lock (Gate)
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Write("AppDomain.UnhandledException", exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Write("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };
    }

    /// <summary>Appends an entry to the crash log. Never throws.</summary>
    public static void Write(string source, Exception exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var builder = new StringBuilder();
            builder.AppendLine(new string('=', 72));
            builder.AppendLine($"{DateTimeOffset.Now:O}  {source}");
            builder.AppendLine(exception.ToString());

            File.AppendAllText(LogPath, builder.ToString(), Encoding.UTF8);
        }
        catch (Exception)
        {
            // There is nothing useful left to do if even the crash log cannot be written.
        }
    }
}
