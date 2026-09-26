using FireReplace.Core.Actions;
using FireReplace.Core.Adb;
using FireReplace.Core.Catalog;

namespace FireReplace.Core.Services;

/// <summary>
/// Turns setting groups into reviewable plans. Nothing here talks to a process directly; every
/// change is expressed as a call into <see cref="IAdbService"/>.
/// </summary>
public sealed class SettingsActionService
{
    private readonly IAdbService _adb;

    /// <summary>Creates the service.</summary>
    public SettingsActionService(IAdbService adb) => _adb = adb ?? throw new ArgumentNullException(nameof(adb));

    /// <summary>Builds a plan that writes every setting in <paramref name="group"/> to its applied value.</summary>
    public ActionPlan BuildApplyPlan(SettingGroup group, DeviceState state)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(state);

        var changes = group.Settings.Select(s => BuildChange(s, state, s.AppliedValue)).ToArray();

        return new ActionPlan(
            $"Apply: {group.Title}",
            group.Description,
            changes,
            Warning: group.Confidence == SettingConfidence.Experimental
                ? "These settings were not part of the verified core configuration. They may not exist on your Fire OS build, and their effect has not been confirmed."
                : null,
            ConfirmLabel: "Apply");
    }

    /// <summary>Builds a plan that returns every setting in <paramref name="group"/> to its default.</summary>
    public ActionPlan BuildRevertPlan(SettingGroup group, DeviceState state)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(state);

        var changes = group.Settings.Select(s => BuildChange(s, state, s.DefaultValue)).ToArray();

        return new ActionPlan(
            $"Revert: {group.Title}",
            $"Returns the {group.Title.ToLowerInvariant()} settings to their Fire OS defaults.",
            changes,
            ConfirmLabel: "Revert");
    }

    /// <summary>Builds a plan for one setting, toggling it to its applied or default value.</summary>
    public ActionPlan BuildTogglePlan(SettingDefinition setting, DeviceState state, bool applied)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(state);

        var target = applied ? setting.AppliedValue : setting.DefaultValue;

        return new ActionPlan(
            $"{(applied ? "Apply" : "Revert")}: {setting.DisplayName}",
            setting.Description,
            [BuildChange(setting, state, target)],
            Warning: setting.Confidence == SettingConfidence.Experimental
                ? "This setting was not part of the verified core configuration."
                : null,
            ConfirmLabel: applied ? "Apply" : "Revert");
    }

    /// <summary>Builds a plan covering several groups at once, used by the recommended setup.</summary>
    public IReadOnlyList<PlannedChange> BuildChanges(IEnumerable<SettingGroup> groups, DeviceState state)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return groups.SelectMany(g => g.Settings).Select(s => BuildChange(s, state, s.AppliedValue)).ToArray();
    }

    /// <summary>Builds a single planned change for a setting.</summary>
    public PlannedChange BuildChange(SettingDefinition setting, DeviceState state, string newValue)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(state);

        var current = state.ValueOf(setting);
        var preview = AdbCommandResult.Format(AdbArguments.PutSetting(_adb.Serial, setting.Scope, setting.Key, newValue));

        return new PlannedChange(
            ChangeKind.SettingWrite,
            setting.DisplayName,
            $"{setting.ScopeToken} {setting.Key}",
            current ?? "(not set)",
            newValue,
            ct => _adb.PutSettingAsync(setting.Scope, setting.Key, newValue, ct),
            preview,
            setting.Description);
    }
}
