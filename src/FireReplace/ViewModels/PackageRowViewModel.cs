using FireReplace.Core.Models;
using FireReplace.Core.Services;
using FireReplace.Mvvm;

namespace FireReplace.ViewModels;

/// <summary>
/// One package in a list. Guarded packages expose no enable/disable affordance at all, so the
/// protection is visible rather than only enforced when a button is pressed.
/// </summary>
public sealed class PackageRowViewModel : ObservableObject
{
    private readonly Action<PackageRowViewModel>? _selectionChanged;
    private PackageInfo _info;
    private bool _isSelected;

    /// <summary>Creates the row.</summary>
    /// <param name="info">Package information.</param>
    /// <param name="selectionChanged">Called when the checkbox changes, so the page can update counts.</param>
    public PackageRowViewModel(PackageInfo info, Action<PackageRowViewModel>? selectionChanged = null)
    {
        _info = info ?? throw new ArgumentNullException(nameof(info));
        _selectionChanged = selectionChanged;
    }

    /// <summary>Package name.</summary>
    public string Name => _info.Name;

    /// <summary>Known description, or a neutral fallback.</summary>
    public string Description => _info.Description ?? "No description available for this package.";

    /// <summary>True when the package has a curated description.</summary>
    public bool HasDescription => _info.Description is not null;

    /// <summary>Current device state.</summary>
    public PackageState State => _info.State;

    /// <summary>Safety classification.</summary>
    public PackageClassification Classification => _info.Classification;

    /// <summary>Label such as "CORE — PROTECTED", or null.</summary>
    public string? GuardLabel => _info.GuardLabel;

    /// <summary>True when FireReplace refuses to disable this package.</summary>
    public bool IsGuarded => _info.IsGuarded;

    /// <summary>Human readable state.</summary>
    public string StateLabel => State switch
    {
        PackageState.Enabled => "Enabled",
        PackageState.Disabled => "Disabled",
        PackageState.NotInstalled => "Not installed",
        _ => "Unknown",
    };

    /// <summary>True when the package exists on the device.</summary>
    public bool IsInstalled => State is PackageState.Enabled or PackageState.Disabled;

    /// <summary>True when disabling is offered.</summary>
    public bool CanDisable => !IsGuarded && State == PackageState.Enabled;

    /// <summary>True when enabling is offered.</summary>
    public bool CanEnable => State == PackageState.Disabled;

    /// <summary>Whether the row is ticked for a bulk action.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (IsGuarded)
            {
                return;
            }

            if (SetProperty(ref _isSelected, value))
            {
                _selectionChanged?.Invoke(this);
            }
        }
    }

    /// <summary>Refreshes the row from a snapshot.</summary>
    public void Update(DeviceState state)
    {
        _info = state.PackageInfoFor(_info.Name);
        OnPropertiesChanged(
            nameof(State),
            nameof(StateLabel),
            nameof(IsInstalled),
            nameof(CanDisable),
            nameof(CanEnable),
            nameof(Classification),
            nameof(GuardLabel),
            nameof(IsGuarded),
            nameof(Description),
            nameof(HasDescription));
    }

    /// <summary>Clears the selection without notifying the owner.</summary>
    public void ResetSelection()
    {
        if (_isSelected)
        {
            _isSelected = false;
            OnPropertyChanged(nameof(IsSelected));
        }
    }
}
