using System.Windows;
using FireReplace.Core.Actions;
using FireReplace.Core.Adb;
using FireReplace.Mvvm;

namespace FireReplace.Dialogs;

/// <summary>
/// Hosts the single overlay dialog. Implements <see cref="IConfirmationHost"/> so the action engine
/// can ask for approval without knowing anything about WPF.
/// </summary>
public sealed class DialogService : ObservableObject, IConfirmationHost
{
    private DialogViewModelBase? _active;
    private Func<bool> _dryRunProvider = () => false;
    private Func<ActionPlan, bool> _confirmationPolicy = _ => true;

    /// <summary>The dialog currently shown, or null.</summary>
    public DialogViewModelBase? Active
    {
        get => _active;
        private set
        {
            if (SetProperty(ref _active, value))
            {
                OnPropertyChanged(nameof(IsOpen));
            }
        }
    }

    /// <summary>True while a dialog is open.</summary>
    public bool IsOpen => _active is not null;

    /// <summary>Tells the service how to discover whether dry run is active.</summary>
    public void UseDryRunProvider(Func<bool> provider) => _dryRunProvider = provider;

    /// <summary>
    /// Installs the policy that decides whether a plan needs an explicit confirmation. Plans that are
    /// destructive, carry a warning, or run under dry run are always confirmed regardless of the policy.
    /// </summary>
    public void UseConfirmationPolicy(Func<ActionPlan, bool> policy) =>
        _confirmationPolicy = policy ?? (_ => true);

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(ActionPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var mustConfirm = plan.IsDestructive || plan.Warning is not null || _dryRunProvider();
        if (!mustConfirm && !_confirmationPolicy(plan))
        {
            return Task.FromResult(true);
        }

        return ShowAsync(new ConfirmationDialogViewModel(plan, _dryRunProvider())
        {
            Title = plan.Title,
        });
    }

    /// <summary>Shows a message overlay and returns the id of the button that was pressed.</summary>
    public async Task<string?> ShowMessageAsync(MessageDialogViewModel dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        await ShowAsync(dialog).ConfigureAwait(true);
        return dialog.SelectedButtonId;
    }

    /// <summary>Shows a simple informational overlay with a single Close button.</summary>
    public Task ShowInfoAsync(string title, string message, string? detail = null, MessageSeverity severity = MessageSeverity.Info) =>
        ShowMessageAsync(new MessageDialogViewModel
        {
            Title = title,
            Message = message,
            Detail = detail,
            Severity = severity,
        });

    /// <summary>
    /// Shows an interpreted ADB failure with its likely causes and the raw output, offering
    /// Retry, Back and Diagnostics.
    /// </summary>
    public Task<string?> ShowFailureAsync(AdbFailure failure, bool offerRetry = true, bool offerDiagnostics = true)
    {
        ArgumentNullException.ThrowIfNull(failure);

        var buttons = new List<MessageDialogButton>();
        if (offerRetry)
        {
            buttons.Add(new MessageDialogButton("Retry", "retry", true));
        }

        buttons.Add(new MessageDialogButton("Back", "back"));

        if (offerDiagnostics)
        {
            buttons.Add(new MessageDialogButton("Diagnostics", "diagnostics"));
        }

        return ShowMessageAsync(new MessageDialogViewModel
        {
            Title = failure.IsProtectedByFireOs ? "Fire OS protected this package" : "Something went wrong",
            Message = failure.Headline,
            Causes = failure.Causes,
            Detail = failure.RawDetail,
            Severity = MessageSeverity.Error,
            Buttons = buttons,
        });
    }

    private async Task<bool> ShowAsync(DialogViewModelBase dialog)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            await dispatcher.InvokeAsync(() => Active = dialog);
        }
        else
        {
            Active = dialog;
        }

        try
        {
            return await dialog.Completion.ConfigureAwait(true);
        }
        finally
        {
            if (ReferenceEquals(Active, dialog))
            {
                Active = null;
            }
        }
    }
}
