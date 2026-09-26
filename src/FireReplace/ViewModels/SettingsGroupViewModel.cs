using System.Collections.ObjectModel;
using FireReplace.Core.Catalog;
using FireReplace.Core.Services;
using FireReplace.Mvvm;
using FireReplace.Services;

namespace FireReplace.ViewModels;

/// <summary>One setting row: what it is, what the device currently reports, and how to change it.</summary>
public sealed class SettingRowViewModel : ObservableObject
{
    private readonly Func<SettingDefinition, bool, CancellationToken, Task> _toggle;
    private string _currentValue = "unknown";
    private bool _isApplied;
    private string _stateLabel = "Unknown";

    /// <summary>Creates the row.</summary>
    internal SettingRowViewModel(
        SettingDefinition definition,
        Func<SettingDefinition, bool, CancellationToken, Task> toggle)
    {
        Definition = definition;
        _toggle = toggle;

        ApplyCommand = new AsyncRelayCommand(ct => _toggle(Definition, true, ct));
        RevertCommand = new AsyncRelayCommand(ct => _toggle(Definition, false, ct));
    }

    /// <summary>The catalog definition behind this row.</summary>
    public SettingDefinition Definition { get; }

    /// <summary>Label.</summary>
    public string DisplayName => Definition.DisplayName;

    /// <summary>Explanation.</summary>
    public string Description => Definition.Description;

    /// <summary>The fully qualified key, shown in monospace.</summary>
    public string QualifiedKey => $"{Definition.ScopeToken} {Definition.Key}";

    /// <summary>True when the setting was not part of the verified configuration.</summary>
    public bool IsExperimental => Definition.Confidence == SettingConfidence.Experimental;

    /// <summary>The raw value read from the device.</summary>
    public string CurrentValue
    {
        get => _currentValue;
        private set => SetProperty(ref _currentValue, value);
    }

    /// <summary>True when the device already holds the applied value.</summary>
    public bool IsApplied
    {
        get => _isApplied;
        private set => SetProperty(ref _isApplied, value);
    }

    /// <summary>Human label for the current state, for example "Limited" or "Default".</summary>
    public string StateLabel
    {
        get => _stateLabel;
        private set => SetProperty(ref _stateLabel, value);
    }

    /// <summary>Writes the applied value.</summary>
    public AsyncRelayCommand ApplyCommand { get; }

    /// <summary>Writes the default value.</summary>
    public AsyncRelayCommand RevertCommand { get; }

    /// <summary>Refreshes the row from a snapshot.</summary>
    public void Update(DeviceState state)
    {
        var value = state.ValueOf(Definition);
        CurrentValue = value ?? "not set";
        IsApplied = state.IsApplied(Definition);
        StateLabel = IsApplied ? Definition.OnLabel : Definition.OffLabel;
    }
}

/// <summary>A group of settings with group-level apply and revert.</summary>
public sealed class SettingGroupViewModel : ObservableObject
{
    private bool _isFullyApplied;
    private int _appliedCount;

    /// <summary>Creates the group.</summary>
    internal SettingGroupViewModel(
        SettingGroup group,
        IEnumerable<SettingRowViewModel> rows,
        Func<SettingGroup, bool, CancellationToken, Task> apply)
    {
        Group = group;
        Rows = new ObservableCollection<SettingRowViewModel>(rows);
        ApplyCommand = new AsyncRelayCommand(ct => apply(Group, true, ct));
        RevertCommand = new AsyncRelayCommand(ct => apply(Group, false, ct));
    }

    /// <summary>The catalog group.</summary>
    public SettingGroup Group { get; }

    /// <summary>Group heading.</summary>
    public string Title => Group.Title;

    /// <summary>Group explanation.</summary>
    public string Description => Group.Description;

    /// <summary>True when the group is experimental.</summary>
    public bool IsExperimental => Group.Confidence == SettingConfidence.Experimental;

    /// <summary>
    /// True for the animation group, which the Performance page presents as a two-option choice
    /// rather than three individual toggles.
    /// </summary>
    public bool SupportsModeSelector => Group.Id == "animations";

    /// <summary>Rows in the group.</summary>
    public ObservableCollection<SettingRowViewModel> Rows { get; }

    /// <summary>True when every setting in the group holds its applied value.</summary>
    public bool IsFullyApplied
    {
        get => _isFullyApplied;
        private set
        {
            if (SetProperty(ref _isFullyApplied, value))
            {
                OnPropertyChanged(nameof(IsDefaultSelected));
            }
        }
    }

    /// <summary>Inverse of <see cref="IsFullyApplied"/>, for the two-option selector.</summary>
    public bool IsDefaultSelected => !IsFullyApplied;

    /// <summary>How many settings in the group are applied.</summary>
    public int AppliedCount
    {
        get => _appliedCount;
        private set
        {
            if (SetProperty(ref _appliedCount, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>Summary such as "2 of 3 applied".</summary>
    public string StatusText => AppliedCount == Rows.Count ? "Applied" : $"{AppliedCount} of {Rows.Count} applied";

    /// <summary>Applies every setting in the group.</summary>
    public AsyncRelayCommand ApplyCommand { get; }

    /// <summary>Reverts every setting in the group.</summary>
    public AsyncRelayCommand RevertCommand { get; }

    /// <summary>Refreshes the group and its rows from a snapshot.</summary>
    public void Update(DeviceState state)
    {
        foreach (var row in Rows)
        {
            row.Update(state);
        }

        AppliedCount = Rows.Count(r => r.IsApplied);
        IsFullyApplied = AppliedCount == Rows.Count && Rows.Count > 0;
        NotifySelectionState();
    }

    /// <summary>
    /// Re-announces the selector state unconditionally. Needed after a cancelled confirmation, because
    /// the radio button has already moved itself and must snap back to what the device actually reports.
    /// </summary>
    public void NotifySelectionState()
    {
        OnPropertyChanged(nameof(IsFullyApplied));
        OnPropertyChanged(nameof(IsDefaultSelected));
    }
}

/// <summary>
/// A page that presents one or more setting groups. Used for Performance, Privacy and the separated
/// Advanced page, so there is a single implementation of "show the value, explain it, confirm changes".
/// </summary>
public sealed class SettingsGroupViewModel : PageViewModel
{
    private readonly string _title;
    private readonly string _subtitle;

    /// <summary>Creates the page.</summary>
    /// <param name="host">Composition root.</param>
    /// <param name="title">Page heading.</param>
    /// <param name="subtitle">Page description.</param>
    /// <param name="groups">Groups to present.</param>
    /// <param name="warning">Optional page-level warning, used by the Advanced page.</param>
    public SettingsGroupViewModel(
        AppHost host,
        string title,
        string subtitle,
        IReadOnlyList<SettingGroup> groups,
        string? warning = null)
        : base(host)
    {
        _title = title;
        _subtitle = subtitle;
        Warning = warning;

        foreach (var group in groups)
        {
            var rows = group.Settings.Select(s => new SettingRowViewModel(s, ToggleSettingAsync));
            Groups.Add(new SettingGroupViewModel(group, rows, ApplyGroupAsync));
        }

        RefreshCommand = new AsyncRelayCommand(ct => RefreshAsync(RefreshScope.Settings, ct));
    }

    /// <inheritdoc />
    public override string Title => _title;

    /// <inheritdoc />
    public override string Subtitle => _subtitle;

    /// <summary>Optional warning banner.</summary>
    public string? Warning { get; }

    /// <summary>The groups on this page.</summary>
    public ObservableCollection<SettingGroupViewModel> Groups { get; } = [];

    /// <summary>Re-reads the settings from the device.</summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <inheritdoc />
    public override void OnDeviceStateChanged(DeviceState state)
    {
        base.OnDeviceStateChanged(state);

        foreach (var group in Groups)
        {
            group.Update(state);
        }
    }

    private async Task ApplyGroupAsync(SettingGroup group, bool applied, CancellationToken cancellationToken)
    {
        if (State is not { } state)
        {
            return;
        }

        var plan = applied
            ? Host.SettingsActions.BuildApplyPlan(group, state)
            : Host.SettingsActions.BuildRevertPlan(group, state);

        await RunPlanAsync(plan, RefreshScope.Settings, cancellationToken).ConfigureAwait(true);

        foreach (var row in Groups)
        {
            row.NotifySelectionState();
        }
    }

    private async Task ToggleSettingAsync(SettingDefinition definition, bool applied, CancellationToken cancellationToken)
    {
        if (State is not { } state)
        {
            return;
        }

        var plan = Host.SettingsActions.BuildTogglePlan(definition, state, applied);
        await RunPlanAsync(plan, RefreshScope.Settings, cancellationToken).ConfigureAwait(true);
    }
}
