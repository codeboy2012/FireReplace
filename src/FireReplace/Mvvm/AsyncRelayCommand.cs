using System.Windows.Input;

namespace FireReplace.Mvvm;

/// <summary>
/// An <see cref="ICommand"/> for asynchronous work. While the operation runs the command reports
/// <c>CanExecute == false</c>, which keeps buttons from being pressed twice and keeps the UI thread free.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, CancellationToken, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private readonly Action<Exception>? _onError;

    private CancellationTokenSource? _cancellation;
    private bool _isRunning;

    /// <summary>Creates a command from a parameterless async action.</summary>
    public AsyncRelayCommand(
        Func<CancellationToken, Task> execute,
        Func<bool>? canExecute = null,
        Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = (_, ct) => execute(ct);
        _canExecute = canExecute is null ? null : _ => canExecute();
        _onError = onError;
    }

    /// <summary>Creates a command that receives the command parameter.</summary>
    public AsyncRelayCommand(
        Func<object?, CancellationToken, Task> execute,
        Func<object?, bool>? canExecute = null,
        Action<Exception>? onError = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onError = onError;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <summary>True while the operation is running.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value)
            {
                return;
            }

            _isRunning = value;
            RaiseCanExecuteChanged();
            IsRunningChanged?.Invoke(this, value);
        }
    }

    /// <summary>Raised when <see cref="IsRunning"/> changes, so view models can show progress.</summary>
    public event EventHandler<bool>? IsRunningChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => !IsRunning && (_canExecute?.Invoke(parameter) ?? true);

    /// <inheritdoc />
    public void Execute(object? parameter) => _ = ExecuteAsync(parameter);

    /// <summary>Runs the command and awaits it, for callers that need the completion.</summary>
    public async Task ExecuteAsync(object? parameter = null)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        IsRunning = true;

        try
        {
            await _execute(parameter, _cancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a normal outcome; the log already records it.
        }
        catch (Exception ex)
        {
            if (_onError is null)
            {
                throw;
            }

            _onError(ex);
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>Cancels the running operation, if any.</summary>
    public void Cancel()
    {
        if (_cancellation is { IsCancellationRequested: false })
        {
            _cancellation.Cancel();
        }
    }

    /// <summary>Asks the UI to re-evaluate <see cref="CanExecute"/>.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
