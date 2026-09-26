using System.Windows;
using System.Windows.Threading;
using FireReplace.Core.Logging;
using FireReplace.Services;
using FireReplace.Views;

namespace FireReplace;

/// <summary>
/// Application entry point. Startup does the minimum required to show the window: read the
/// configuration file, probe for adb.exe and the APK folder, then create the shell. No device
/// communication happens until the user asks for it.
/// </summary>
public partial class App : Application
{
    private AppHost? _host;
    private bool _reportedToUser;

    /// <summary>
    /// Runs before the application instance is constructed, so a failure while loading resources is
    /// still recorded instead of vanishing behind an exit code.
    /// </summary>
    static App() => CrashReporter.Install();

    /// <summary>The composition root, available to views that need it.</summary>
    public static AppHost Host =>
        (Current as App)?._host ?? throw new InvalidOperationException("The application host is not ready.");

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                TryLogFatal(exception);
            }
        };

        var directory = AppContext.BaseDirectory;
        _host = new AppHost(directory);

        ThemeManager.Apply(_host.Config.Theme);

        _host.Log.Write(LogLevel.Info, "app", $"FireReplace started from {directory}");
        _host.Log.Write(
            _host.AdbLocation.Found ? LogLevel.Success : LogLevel.Warning,
            "app",
            _host.AdbLocation.Found
                ? $"adb.exe found at {_host.AdbLocation.AdbPath}"
                : "adb.exe was not found. FireReplace will show the setup screen.");

        var window = new MainWindow
        {
            DataContext = _host.Shell,
        };

        MainWindow = window;
        window.Show();

        _ = _host.Shell.InitializeAsync();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.Shell.PersistState();
            _host.SaveConfig();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        TryLogFatal(e.Exception);
        CrashReporter.Write("Dispatcher", e.Exception);

        // Showing a dialog pumps messages, which can re-enter this handler if the original failure
        // came from layout. One report per session is enough; the rest go to the crash log.
        if (!_reportedToUser)
        {
            _reportedToUser = true;

            MessageBox.Show(
                $"FireReplace hit an unexpected error and may not be in a usable state.\n\n{e.Exception.Message}\n\n"
                + $"No further device commands were sent. Details were written to:\n{CrashReporter.LogPath}",
                "FireReplace",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        e.Handled = true;
    }

    private void TryLogFatal(Exception exception)
    {
        try
        {
            _host?.Log.Write(LogLevel.Error, "app", "Unhandled error", exception.ToString());
        }
        catch (Exception)
        {
            // Logging must never mask the original failure.
        }
    }
}
