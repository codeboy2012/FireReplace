using FireReplace.Core.Adb;
using FireReplace.Core.Validation;

namespace FireReplace.Core.Services;

/// <summary>Result of merging a component into an existing colon-separated list.</summary>
/// <param name="Current">The list as it exists on the device right now.</param>
/// <param name="Proposed">The list FireReplace would write.</param>
/// <param name="Added">Components that would be added.</param>
/// <param name="Rejected">Components that were skipped because they failed validation.</param>
public sealed record ComponentMergeResult(
    IReadOnlyList<string> Current,
    IReadOnlyList<string> Proposed,
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Rejected)
{
    /// <summary>True when the proposed list differs from the current one.</summary>
    public bool HasChanges => Added.Count > 0;

    /// <summary>The value that would be written with <c>settings put</c>.</summary>
    public string ProposedValue => string.Join(':', Proposed);

    /// <summary>The value currently stored on the device.</summary>
    public string CurrentValue => string.Join(':', Current);
}

/// <summary>
/// Additive merging of component lists such as <c>enabled_accessibility_services</c>.
/// </summary>
/// <remarks>
/// This is the single most destructive-by-accident operation in the original workflow: writing the
/// list wholesale would silently switch off Home on Fire's Home-button hijack. The merge is
/// therefore strictly additive, preserves order, and never removes an entry it does not recognise.
/// </remarks>
public static class ComponentListMerge
{
    /// <summary>
    /// Adds <paramref name="componentsToAdd"/> to the list parsed from <paramref name="currentRawValue"/>
    /// without removing or reordering anything that is already there.
    /// </summary>
    public static ComponentMergeResult Add(string? currentRawValue, IEnumerable<string> componentsToAdd)
    {
        ArgumentNullException.ThrowIfNull(componentsToAdd);

        var current = AdbOutputParser.ParseComponentList(currentRawValue);
        var proposed = new List<string>(current);
        var added = new List<string>();
        var rejected = new List<string>();

        foreach (var component in componentsToAdd)
        {
            if (!Validate.IsComponentName(component))
            {
                rejected.Add(component ?? "(empty)");
                continue;
            }

            if (proposed.Contains(component, StringComparer.Ordinal))
            {
                continue;
            }

            proposed.Add(component);
            added.Add(component);
        }

        return new ComponentMergeResult(current, proposed, added, rejected);
    }

    /// <summary>
    /// Removes <paramref name="componentsToRemove"/> from the list, leaving every other entry intact.
    /// Used only by explicit user-driven revert actions.
    /// </summary>
    public static ComponentMergeResult Remove(string? currentRawValue, IEnumerable<string> componentsToRemove)
    {
        ArgumentNullException.ThrowIfNull(componentsToRemove);

        var current = AdbOutputParser.ParseComponentList(currentRawValue);
        var removeSet = new HashSet<string>(componentsToRemove, StringComparer.Ordinal);
        var proposed = current.Where(c => !removeSet.Contains(c)).ToArray();
        var removed = current.Where(removeSet.Contains).ToArray();

        return new ComponentMergeResult(current, proposed, removed, Array.Empty<string>());
    }
}
