using FireReplace.Core.Adb;
using FireReplace.Core.Catalog;
using FireReplace.Core.Logging;

namespace FireReplace.Core.Actions;

/// <summary>Result of a single change inside an executed plan.</summary>
/// <param name="Change">The change that was attempted.</param>
/// <param name="Succeeded">True when the device accepted it, or when dry run skipped it.</param>
/// <param name="WasDryRun">True when nothing was sent to the device.</param>
/// <param name="Failure">Interpreted failure, when it failed.</param>
public sealed record ChangeOutcome(
    PlannedChange Change,
    bool Succeeded,
    bool WasDryRun,
    AdbFailure? Failure);

/// <summary>Result of executing a whole plan.</summary>
/// <param name="Plan">The plan that ran.</param>
/// <param name="Outcomes">Per-change outcomes, in execution order.</param>
/// <param name="Cancelled">True when the user or the app cancelled part way through.</param>
/// <param name="WasDryRun">True when the plan ran entirely in dry-run mode.</param>
public sealed record ActionReport(
    ActionPlan Plan,
    IReadOnlyList<ChangeOutcome> Outcomes,
    bool Cancelled,
    bool WasDryRun)
{
    /// <summary>Number of changes the device accepted.</summary>
    public int SucceededCount => Outcomes.Count(o => o.Succeeded);

    /// <summary>Number of changes that failed.</summary>
    public int FailedCount => Outcomes.Count(o => !o.Succeeded);

    /// <summary>Failures caused by Fire OS protecting a package.</summary>
    public IReadOnlyList<ChangeOutcome> ProtectedFailures =>
        Outcomes.Where(o => o.Failure?.IsProtectedByFireOs == true).ToArray();

    /// <summary>True when nothing failed.</summary>
    public bool AllSucceeded => FailedCount == 0 && !Cancelled;

    /// <summary>A short summary line for the log and the UI banner.</summary>
    public string Summary =>
        WasDryRun
            ? $"Dry run: {Outcomes.Count} change(s) would have been applied. No changes were made."
            : Cancelled
                ? $"Cancelled after {SucceededCount} of {Plan.EffectiveChanges.Count} change(s)."
                : FailedCount == 0
                    ? $"{SucceededCount} change(s) applied."
                    : $"{SucceededCount} applied, {FailedCount} failed.";
}

/// <summary>Asks the user to approve a plan. Implemented by the UI layer.</summary>
public interface IConfirmationHost
{
    /// <summary>
    /// Shows <paramref name="plan"/> and returns true when the user approves it.
    /// </summary>
    Task<bool> ConfirmAsync(ActionPlan plan, CancellationToken cancellationToken = default);
}

/// <summary>
/// The single path through which every device modification runs.
/// </summary>
/// <remarks>
/// <para>The sequence is always the same:</para>
/// <list type="number">
/// <item><description>Reject changes that touch guarded packages.</description></item>
/// <item><description>Drop changes that are already satisfied.</description></item>
/// <item><description>Ask the user to confirm, showing exact current and new values.</description></item>
/// <item><description>Execute one change at a time, capturing stdout, stderr and the exit code.</description></item>
/// <item><description>Report per-change success or an interpreted failure.</description></item>
/// </list>
/// </remarks>
public sealed class ActionEngine
{
    private readonly IAdbService _adb;
    private readonly ILogService _log;
    private readonly IConfirmationHost _confirmation;

    /// <summary>Creates the engine.</summary>
    public ActionEngine(IAdbService adb, ILogService log, IConfirmationHost confirmation)
    {
        _adb = adb ?? throw new ArgumentNullException(nameof(adb));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
    }

    /// <summary>Raised when a plan finishes, so pages can refresh the state they show.</summary>
    public event EventHandler<ActionReport>? PlanCompleted;

    /// <summary>
    /// Validates, confirms and runs a plan. Returns null when the user declined or there was
    /// nothing to do.
    /// </summary>
    public async Task<ActionReport?> RunAsync(ActionPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var guardViolations = plan.Changes
            .Where(c => c.Kind is ChangeKind.PackageDisable && PackageCatalog.IsGuarded(c.Target))
            .ToArray();

        if (guardViolations.Length > 0)
        {
            var names = string.Join(", ", guardViolations.Select(c => c.Target));
            _log.Write(LogLevel.Warning, "safety", $"Refused to disable protected package(s): {names}");
            throw new ProtectedPackageException(names);
        }

        var effective = plan.EffectiveChanges;
        if (effective.Count == 0)
        {
            _log.Write(LogLevel.Info, "action", $"{plan.Title}: already up to date, nothing to change.");
            return null;
        }

        var runnablePlan = plan with { Changes = effective };

        if (!await _confirmation.ConfirmAsync(runnablePlan, cancellationToken).ConfigureAwait(false))
        {
            _log.Write(LogLevel.Info, "action", $"{plan.Title}: cancelled before any change was made.");
            return null;
        }

        var dryRun = _adb.DryRun;
        _log.Write(
            dryRun ? LogLevel.DryRun : LogLevel.Info,
            "action",
            dryRun
                ? $"DRY RUN — {plan.Title}: {effective.Count} change(s) would be applied."
                : $"{plan.Title}: applying {effective.Count} change(s).");

        var outcomes = new List<ChangeOutcome>(effective.Count);
        var cancelled = false;

        foreach (var change in effective)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            try
            {
                var result = await change.Execute(cancellationToken).ConfigureAwait(false);
                var succeeded = result.Success && !IndicatesTextualFailure(change, result);

                if (succeeded)
                {
                    _log.Write(
                        result.WasDryRun ? LogLevel.DryRun : LogLevel.Success,
                        "action",
                        result.WasDryRun
                            ? $"Would {Describe(change)}"
                            : $"{Describe(change)}");
                    outcomes.Add(new ChangeOutcome(change, true, result.WasDryRun, null));
                }
                else
                {
                    var failure = AdbErrorInterpreter.Interpret(result, Describe(change));
                    _log.Write(LogLevel.Error, "action", failure.Headline, failure.RawDetail);
                    outcomes.Add(new ChangeOutcome(change, false, result.WasDryRun, failure));
                }
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                _log.Write(LogLevel.Warning, "action", $"{plan.Title}: cancelled during '{change.Target}'.");
                break;
            }
            catch (Exception ex)
            {
                var failure = new AdbFailure(
                    $"FireReplace could not {Describe(change)}.",
                    ["An unexpected error occurred before the command reached the device."],
                    ex.Message);
                _log.Write(LogLevel.Error, "action", failure.Headline, ex.Message);
                outcomes.Add(new ChangeOutcome(change, false, false, failure));
            }
        }

        var report = new ActionReport(runnablePlan, outcomes, cancelled, dryRun);
        _log.Write(
            report.AllSucceeded ? LogLevel.Success : LogLevel.Warning,
            "action",
            $"{plan.Title}: {report.Summary}");

        PlanCompleted?.Invoke(this, report);
        return report;
    }

    /// <summary>
    /// <c>pm</c> and <c>cmd notification</c> often exit 0 while printing a failure, so the text is
    /// checked as well as the exit code.
    /// </summary>
    private static bool IndicatesTextualFailure(PlannedChange change, AdbCommandResult result)
    {
        if (result.WasDryRun)
        {
            return false;
        }

        var text = result.CombinedOutput;
        if (AdbOutputParser.IndicatesSecurityException(text))
        {
            return true;
        }

        return change.Kind switch
        {
            ChangeKind.ApkInstall => !AdbOutputParser.IndicatesInstallSuccess(text),
            ChangeKind.PackageDisable or ChangeKind.PackageEnable =>
                text.Contains("Unknown package", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Error:", StringComparison.OrdinalIgnoreCase),
            ChangeKind.PermissionGrant or ChangeKind.NotificationListener =>
                text.Contains("Exception", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Error:", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static string Describe(PlannedChange change) => change.Kind switch
    {
        ChangeKind.PackageDisable => $"disable {change.Target}",
        ChangeKind.PackageEnable => $"enable {change.Target}",
        ChangeKind.SettingWrite => $"set {change.Target} to {change.NewValue}",
        ChangeKind.ApkInstall => $"install {change.Target}",
        ChangeKind.PermissionGrant => $"grant {change.NewValue} to {change.Target}",
        ChangeKind.NotificationListener => $"allow notification access for {change.Target}",
        ChangeKind.AccessibilityList => "update the accessibility service list",
        ChangeKind.Reboot => "reboot the Fire TV",
        _ => change.Title.ToLowerInvariant(),
    };
}

/// <summary>Thrown when a plan tries to disable a package FireReplace guards.</summary>
public sealed class ProtectedPackageException : InvalidOperationException
{
    /// <summary>Creates the exception for the given package names.</summary>
    public ProtectedPackageException(string packageNames)
        : base($"FireReplace refuses to disable protected package(s): {packageNames}.")
    {
        PackageNames = packageNames;
    }

    /// <summary>The offending package names.</summary>
    public string PackageNames { get; }
}
