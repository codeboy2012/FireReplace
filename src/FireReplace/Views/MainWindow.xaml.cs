using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using FireReplace.ViewModels;

namespace FireReplace.Views;

/// <summary>
/// The application window. Custom chrome is used for the modern look; resizing, snapping and
/// maximising are still handled by <see cref="System.Windows.Shell.WindowChrome"/> so the window
/// behaves like any other Windows window.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Creates the window and wires the keyboard shortcuts.</summary>
    public MainWindow()
    {
        InitializeComponent();

        StateChanged += (_, _) => UpdateMaximizeGlyph();

        InputBindings.Add(new KeyBinding
        {
            Key = Key.F5,
            Command = new RelayCommandAdapter(() => (DataContext as ShellViewModel)?.RefreshCommand.Execute(null)),
        });

        InputBindings.Add(new KeyBinding
        {
            Key = Key.L,
            Modifiers = ModifierKeys.Control,
            Command = new RelayCommandAdapter(() => (DataContext as ShellViewModel)?.Log.ToggleExpandCommand.Execute(null)),
        });

        InputBindings.Add(new KeyBinding
        {
            Key = Key.D,
            Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
            Command = new RelayCommandAdapter(() => (DataContext as ShellViewModel)?.ToggleDryRunCommand.Execute(null)),
        });
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void UpdateMaximizeGlyph()
    {
        // E922 = maximize, E923 = restore (Segoe MDL2 Assets).
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
    }

    /// <summary>A tiny ICommand shim so key bindings can call a plain delegate.</summary>
    private sealed class RelayCommandAdapter(Action action) : ICommand
    {
        private readonly Action _action = action;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _action();
    }
}
