using FireReplace.Core.Adb;

namespace FireReplace.Core.Actions;

/// <summary>The kind of modification a planned change represents.</summary>
public enum ChangeKind
{
    /// <summary>Write a value with <c>settings put</c>.</summary>
    SettingWrite,

    /// <summary>Disable a package with <c>pm disable-user</c>.</summary>
    PackageDisable,

    /// <summary>Enable a package with <c>pm enable</c>.</summary>
    PackageEnable,

    /// <summary>Install an APK.</summary>
    ApkInstall,

    /// <summary>Grant a permission with <c>pm grant</c>.</summary>
    PermissionGrant,

    /// <summary>Allow a notification listener component.</summary>
    NotificationListener,

    /// <summary>Rewrite the accessibility service list (additively).</summary>
    AccessibilityList,

    /// <summary>Reboot the device.</summary>
    Reboot,
}

/// <summary>
/// One concrete, reviewable modification: what is being changed, its current value, its new value,
/// and the exact call that performs it.
/// </summary>
public sealed class PlannedChange
{
    /// <summary>Creates a planned change.</summary>
    /// <param name="kind">Category of change.</param>
    /// <param name="title">Short label, for example "Disable package".</param>
    /// <param name="target">The package name, settings key or file name being changed.</param>
    /// <param name="currentValue">Value as it is on the device right now.</param>
    /// <param name="newValue">Value FireReplace will write.</param>
    /// <param name="execute">The operation itself. Must go through <see cref="IAdbService"/>.</param>
    /// <param name="commandPreview">The adb command that will run, for the preview and dry run.</param>
    /// <param name="detail">Optional extra explanation shown in the confirmation panel.</param>
    public PlannedChange(
        ChangeKind kind,
        string title,
        string target,
        string currentValue,
        string newValue,
        Func<CancellationToken, Task<AdbCommandResult>> execute,
        string? commandPreview = null,
        string? detail = null)
    {
        Kind = kind;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        CurrentValue = currentValue;
        NewValue = newValue;
        Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        CommandPreview = commandPreview;
        Detail = detail;
    }

    /// <summary>Category of change.</summary>
    public ChangeKind Kind { get; }

    /// <summary>Short label.</summary>
    public string Title { get; }

    /// <summary>Package name, settings key or file name.</summary>
    public string Target { get; }

    /// <summary>Value currently on the device.</summary>
    public string CurrentValue { get; }

    /// <summary>Value that will be written.</summary>
    public string NewValue { get; }

    /// <summary>Extra explanation for the confirmation panel.</summary>
    public string? Detail { get; }

    /// <summary>The adb command as it will appear, used for previews and dry run.</summary>
    public string? CommandPreview { get; }

    /// <summary>The operation.</summary>
    public Func<CancellationToken, Task<AdbCommandResult>> Execute { get; }

    /// <summary>True when the change is a no-op because the device already has the target value.</summary>
    public bool IsNoOp => string.Equals(CurrentValue, NewValue, StringComparison.Ordinal);
}

/// <summary>
/// A reviewable group of changes. Nothing is executed until the user approves the plan.
/// </summary>
/// <param name="Title">Plan heading, for example "Disable 18 packages".</param>
/// <param name="Description">What the plan does and why.</param>
/// <param name="Changes">The individual changes, in execution order.</param>
/// <param name="Warning">Optional warning shown prominently, used for experimental settings.</param>
/// <param name="ConfirmLabel">Label for the confirm button.</param>
/// <param name="IsDestructive">Marks plans that change package state or reboot the device.</param>
public sealed record ActionPlan(
    string Title,
    string Description,
    IReadOnlyList<PlannedChange> Changes,
    string? Warning = null,
    string ConfirmLabel = "Apply",
    bool IsDestructive = false)
{
    /// <summary>Changes that would actually alter the device.</summary>
    public IReadOnlyList<PlannedChange> EffectiveChanges => Changes.Where(c => !c.IsNoOp).ToArray();

    /// <summary>True when every change is already satisfied.</summary>
    public bool IsEmpty => EffectiveChanges.Count == 0;
}
