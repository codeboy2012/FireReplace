using FireReplace.Core.Actions;
using FireReplace.Core.Adb;
using FireReplace.Core.Services;
using FireReplace.Dialogs;
using FireReplace.Mvvm;
using FireReplace.Services;

namespace FireReplace.ViewModels;

/// <summary>
/// Base class for the pages hosted by the shell. Pages never talk to a process directly: they build
/// <see cref="ActionPlan"/> objects and hand them to the action engine.
/// </summary>
public abstract class PageViewModel : ObservableObject
{
    private bool _isBusy;

    /// <summary>Creates the page.</summary>
    /// <param name="host">Composition root.</param>
    protected PageViewModel(AppHost host)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>Composition root.</summary>
    protected AppHost Host { get; }

    /// <summary>Overlay dialogs.</summary>
    protected DialogService Dialogs => Host.Dialogs;

    /// <summary>The latest device snapshot, or null before the first refresh.</summary>
    protected DeviceState? State => Host.DeviceStates.Current;

    /// <summary>Page heading.</summary>
    public abstract string Title { get; }

    /// <summary>One-line description shown under the heading.</summary>
    public abstract string Subtitle { get; }

    /// <summary>True while the page is running an operation.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        protected set => SetProperty(ref _isBusy, value);
    }

    /// <summary>True when dry run is active, so pages can show the banner.</summary>
    public bool IsDryRun => Host.Adb.DryRun;

    /// <summary>Called when the page becomes visible.</summary>
    public virtual Task ActivateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>Called whenever a new device snapshot is available.</summary>
    public virtual void OnDeviceStateChanged(DeviceState state)
    {
        OnPropertyChanged(nameof(IsDryRun));
    }

    /// <summary>
    /// Runs a plan through the action engine and reports the outcome, refreshing the affected state.
    /// </summary>
    /// <param name="plan">The plan to run.</param>
    /// <param name="refresh">What to re-read afterwards.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task<ActionReport?> RunPlanAsync(
        ActionPlan plan,
        RefreshScope refresh,
        CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            ActionReport? report;
            try
            {
                report = await Host.Actions.RunAsync(plan, cancellationToken).ConfigureAwait(true);
            }
            catch (ProtectedPackageException ex)
            {
                await Dialogs.ShowInfoAsync(
                    "Protected package",
                    ex.Message,
                    "FireReplace will not attempt to bypass Fire OS package protection.",
                    MessageSeverity.Warning).ConfigureAwait(true);
                return null;
            }

            if (report is null)
            {
                return null;
            }

            await RefreshAsync(refresh, cancellationToken).ConfigureAwait(true);
            await ReportAsync(report).ConfigureAwait(true);
            return report;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Re-reads the requested part of the device state.</summary>
    protected async Task RefreshAsync(RefreshScope scope, CancellationToken cancellationToken = default)
    {
        if (scope == RefreshScope.None || Host.Connection.Status != ConnectionStatus.Connected)
        {
            return;
        }

        switch (scope)
        {
            case RefreshScope.Settings:
                await Host.DeviceStates.RefreshSettingsAsync(cancellationToken).ConfigureAwait(true);
                break;
            case RefreshScope.Packages:
                await Host.DeviceStates.RefreshPackagesAsync(cancellationToken).ConfigureAwait(true);
                break;
            default:
                await Host.DeviceStates.RefreshAsync(cancellationToken).ConfigureAwait(true);
                break;
        }
    }

    /// <summary>Shows the result of a plan, including any Fire OS protection refusals.</summary>
    protected async Task ReportAsync(ActionReport report)
    {
        if (report.WasDryRun)
        {
            var lines = report.Plan.Changes
                .Select(c => c.CommandPreview ?? $"{c.Title}: {c.Target} -> {c.NewValue}")
                .ToArray();

            await Dialogs.ShowMessageAsync(new MessageDialogViewModel
            {
                Title = "Dry run",
                Message = $"FireReplace would run {lines.Length} command(s). No changes were made.",
                Detail = string.Join(System.Environment.NewLine, lines),
                Severity = MessageSeverity.Info,
            }).ConfigureAwait(true);
            return;
        }

        if (report.AllSucceeded)
        {
            return;
        }

        var protectedFailures = report.ProtectedFailures;
        if (protectedFailures.Count == report.FailedCount && protectedFailures.Count > 0)
        {
            await Dialogs.ShowMessageAsync(new MessageDialogViewModel
            {
                Title = "Fire OS protected this package",
                Message = protectedFailures.Count == 1
                    ? $"Fire OS refused to change {protectedFailures[0].Change.Target}."
                    : $"Fire OS refused to change {protectedFailures.Count} packages.",
                Causes =
                [
                    "FireReplace will not attempt to bypass that protection.",
                    "This is a platform restriction, not a FireReplace limitation.",
                    $"{report.SucceededCount} other change(s) were applied.",
                ],
                Detail = string.Join(
                    System.Environment.NewLine,
                    protectedFailures.Select(f => $"{f.Change.Target}: {f.Failure?.RawDetail}")),
                Severity = MessageSeverity.Warning,
            }).ConfigureAwait(true);
            return;
        }

        var failed = report.Outcomes.Where(o => !o.Succeeded).ToArray();
        await Dialogs.ShowMessageAsync(new MessageDialogViewModel
        {
            Title = "Some changes did not apply",
            Message = report.Summary,
            Causes = failed.Select(f => f.Failure?.Headline ?? "Unknown failure").Distinct().ToArray(),
            Detail = string.Join(
                System.Environment.NewLine + System.Environment.NewLine,
                failed.Select(f => $"{f.Change.Target}{System.Environment.NewLine}{f.Failure?.RawDetail}")),
            Severity = MessageSeverity.Error,
            Buttons = [new MessageDialogButton("Close", "close", true)],
        }).ConfigureAwait(true);
    }
}

/// <summary>What to re-read after an action.</summary>
public enum RefreshScope
{
    /// <summary>Nothing needs re-reading.</summary>
    None,

    /// <summary>Re-read the settings values.</summary>
    Settings,

    /// <summary>Re-read the package lists.</summary>
    Packages,

    /// <summary>Re-read everything.</summary>
    All,
}
