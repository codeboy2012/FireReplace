using FireReplace.Core.Actions;
using FireReplace.Mvvm;

namespace FireReplace.Dialogs;

/// <summary>Base class for the overlay dialogs. Overlays are used instead of separate windows so
/// they open instantly and stay inside the application's visual language.</summary>
public abstract class DialogViewModelBase : ObservableObject
{
    private readonly TaskCompletionSource<bool> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the dialog is dismissed. True means the user confirmed.</summary>
    public Task<bool> Completion => _completion.Task;

    /// <summary>Dialog heading.</summary>
    public required string Title { get; init; }

    /// <summary>Closes the dialog with the given result.</summary>
    public void Close(bool result) => _completion.TrySetResult(result);
}

/// <summary>One row in the change preview table.</summary>
/// <param name="Title">Short label for the change.</param>
/// <param name="Target">Package name or settings key.</param>
/// <param name="CurrentValue">Value on the device now.</param>
/// <param name="NewValue">Value that will be written.</param>
/// <param name="Command">The adb command that will run.</param>
/// <param name="Detail">Optional explanation.</param>
public sealed record ChangePreviewRow(
    string Title,
    string Target,
    string CurrentValue,
    string NewValue,
    string? Command,
    string? Detail);

/// <summary>The confirmation overlay for an <see cref="ActionPlan"/>.</summary>
public sealed class ConfirmationDialogViewModel : DialogViewModelBase
{
    /// <summary>Creates the view model from a plan.</summary>
    /// <param name="plan">The plan awaiting approval.</param>
    /// <param name="isDryRun">True when dry run is active, which changes the wording and buttons.</param>
    public ConfirmationDialogViewModel(ActionPlan plan, bool isDryRun)
    {
        ArgumentNullException.ThrowIfNull(plan);

        Plan = plan;
        IsDryRun = isDryRun;
        Rows = plan.Changes
            .Select(c => new ChangePreviewRow(c.Title, c.Target, c.CurrentValue, c.NewValue, c.CommandPreview, c.Detail))
            .ToArray();

        ConfirmCommand = new RelayCommand(() => Close(true));
        CancelCommand = new RelayCommand(() => Close(false));
    }

    /// <summary>The plan being confirmed.</summary>
    public ActionPlan Plan { get; }

    /// <summary>True when nothing will actually be written.</summary>
    public bool IsDryRun { get; }

    /// <summary>Preview rows shown in the dialog.</summary>
    public IReadOnlyList<ChangePreviewRow> Rows { get; }

    /// <summary>Plan description.</summary>
    public string Description => Plan.Description;

    /// <summary>Optional warning banner text.</summary>
    public string? Warning => Plan.Warning;

    /// <summary>Label of the confirm button.</summary>
    public string ConfirmLabel => IsDryRun ? "Show dry run" : Plan.ConfirmLabel;

    /// <summary>Number of changes in the plan.</summary>
    public int ChangeCount => Rows.Count;

    /// <summary>Headline used above the preview table.</summary>
    public string CountLabel => ChangeCount == 1 ? "1 change" : $"{ChangeCount} changes";

    /// <summary>Approves the plan.</summary>
    public RelayCommand ConfirmCommand { get; }

    /// <summary>Rejects the plan.</summary>
    public RelayCommand CancelCommand { get; }
}

/// <summary>A button offered by a message overlay.</summary>
/// <param name="Label">Button text.</param>
/// <param name="Id">Identifier returned to the caller.</param>
/// <param name="IsPrimary">True to style the button as the primary action.</param>
public sealed record MessageDialogButton(string Label, string Id, bool IsPrimary = false);

/// <summary>A message overlay used for errors, explanations and results.</summary>
public sealed class MessageDialogViewModel : DialogViewModelBase
{
    /// <summary>Creates the view model.</summary>
    public MessageDialogViewModel()
    {
        ChooseCommand = new RelayCommand(parameter =>
        {
            SelectedButtonId = parameter as string;
            Close(SelectedButtonId is not null and not "cancel" and not "back");
        });
    }

    /// <summary>Main message text.</summary>
    public required string Message { get; init; }

    /// <summary>Optional bulleted list of likely causes or next steps.</summary>
    public IReadOnlyList<string> Causes { get; init; } = Array.Empty<string>();

    /// <summary>Raw technical detail, shown in a monospaced block. Never hidden from the user.</summary>
    public string? Detail { get; init; }

    /// <summary>Severity marker used to colour the heading.</summary>
    public MessageSeverity Severity { get; init; } = MessageSeverity.Info;

    /// <summary>Buttons to offer.</summary>
    public IReadOnlyList<MessageDialogButton> Buttons { get; init; } =
        [new MessageDialogButton("Close", "close", true)];

    /// <summary>Identifier of the button the user pressed.</summary>
    public string? SelectedButtonId { get; private set; }

    /// <summary>Records the pressed button and closes the overlay.</summary>
    public RelayCommand ChooseCommand { get; }
}

/// <summary>Severity of a message overlay.</summary>
public enum MessageSeverity
{
    /// <summary>Neutral information.</summary>
    Info,

    /// <summary>Operation succeeded.</summary>
    Success,

    /// <summary>Something needs attention.</summary>
    Warning,

    /// <summary>Operation failed.</summary>
    Error,
}
