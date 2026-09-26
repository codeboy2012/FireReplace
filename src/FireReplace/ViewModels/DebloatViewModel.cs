using System.Collections.ObjectModel;
using FireReplace.Core.Catalog;
using FireReplace.Core.Models;
using FireReplace.Core.Services;
using FireReplace.Dialogs;
using FireReplace.Mvvm;
using FireReplace.Services;

namespace FireReplace.ViewModels;

/// <summary>
/// The debloat page: the verified package list with live state, plus a visible, read-only view of the
/// packages FireReplace protects.
/// </summary>
/// <remarks>
/// Nothing is pre-selected beyond the packages that are present and still enabled, and nothing is
/// applied without the standard confirmation. Core and Fire OS protected packages are listed
/// separately and cannot be selected at all.
/// </remarks>
public sealed class DebloatViewModel : PageViewModel
{
    private int _selectedCount;
    private string _summary = "No device snapshot yet.";

    /// <summary>Creates the page.</summary>
    public DebloatViewModel(AppHost host)
        : base(host)
    {
        foreach (var entry in PackageCatalog.VerifiedDebloat)
        {
            Packages.Add(new PackageRowViewModel(
                new PackageInfo
                {
                    Name = entry.Name,
                    Classification = entry.Classification,
                    Description = entry.Description,
                    State = PackageState.Unknown,
                },
                OnSelectionChanged));
        }

        foreach (var entry in PackageCatalog.CoreProtected.Concat(PackageCatalog.FireOsProtected))
        {
            ProtectedPackages.Add(new PackageRowViewModel(new PackageInfo
            {
                Name = entry.Name,
                Classification = entry.Classification,
                Description = entry.Description,
                State = PackageState.Unknown,
            }));
        }

        SelectAllCommand = new RelayCommand(() => SetSelection(true));
        SelectNoneCommand = new RelayCommand(() => SetSelection(false));
        ReviewCommand = new AsyncRelayCommand(ReviewAsync, () => SelectedCount > 0);
        DisableSelectedCommand = new AsyncRelayCommand(DisableSelectedAsync, () => SelectedCount > 0);
        EnableSelectedCommand = new AsyncRelayCommand(EnableSelectedAsync, () => SelectedCount > 0);
        RefreshCommand = new AsyncRelayCommand(ct => RefreshAsync(RefreshScope.Packages, ct));
    }

    /// <inheritdoc />
    public override string Title => "Debloat";

    /// <inheritdoc />
    public override string Subtitle =>
        "The package list verified on the reference Fire TV. Packages are disabled for user 0, never uninstalled, and can be re-enabled at any time.";

    /// <summary>The verified debloat candidates.</summary>
    public ObservableCollection<PackageRowViewModel> Packages { get; } = [];

    /// <summary>Packages FireReplace protects, shown for transparency.</summary>
    public ObservableCollection<PackageRowViewModel> ProtectedPackages { get; } = [];

    /// <summary>How many rows are ticked.</summary>
    public int SelectedCount
    {
        get => _selectedCount;
        private set
        {
            if (SetProperty(ref _selectedCount, value))
            {
                OnPropertyChanged(nameof(SelectionLabel));
                ReviewCommand.RaiseCanExecuteChanged();
                DisableSelectedCommand.RaiseCanExecuteChanged();
                EnableSelectedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Label such as "18 packages selected".</summary>
    public string SelectionLabel =>
        SelectedCount == 1 ? "1 package selected" : $"{SelectedCount} packages selected";

    /// <summary>State summary for the header.</summary>
    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    /// <summary>Ticks every selectable row.</summary>
    public RelayCommand SelectAllCommand { get; }

    /// <summary>Clears every tick.</summary>
    public RelayCommand SelectNoneCommand { get; }

    /// <summary>Shows exactly what would run, without changing anything.</summary>
    public AsyncRelayCommand ReviewCommand { get; }

    /// <summary>Disables the ticked packages.</summary>
    public AsyncRelayCommand DisableSelectedCommand { get; }

    /// <summary>Re-enables the ticked packages.</summary>
    public AsyncRelayCommand EnableSelectedCommand { get; }

    /// <summary>Re-reads package state.</summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <inheritdoc />
    public override void OnDeviceStateChanged(DeviceState state)
    {
        base.OnDeviceStateChanged(state);

        foreach (var row in Packages.Concat(ProtectedPackages))
        {
            row.Update(state);
        }

        // Pre-select only what is actually actionable: present and still enabled.
        foreach (var row in Packages)
        {
            row.IsSelected = row.CanDisable;
        }

        RecountSelection();

        var present = Packages.Count(p => p.IsInstalled);
        var disabled = Packages.Count(p => p.State == PackageState.Disabled);
        var absent = Packages.Count - present;

        Summary = present == 0
            ? "None of the verified packages were found on this device."
            : $"{present} of {Packages.Count} found on this device · {disabled} already disabled"
              + (absent > 0 ? $" · {absent} not present" : string.Empty);
    }

    private void OnSelectionChanged(PackageRowViewModel row) => RecountSelection();

    private void RecountSelection() => SelectedCount = Packages.Count(p => p.IsSelected);

    private void SetSelection(bool selected)
    {
        foreach (var row in Packages)
        {
            row.IsSelected = selected && !row.IsGuarded && row.IsInstalled;
        }

        RecountSelection();
    }

    private async Task ReviewAsync(CancellationToken cancellationToken)
    {
        if (State is not { } state)
        {
            return;
        }

        var selected = Packages.Where(p => p.IsSelected).ToArray();
        var lines = selected
            .Select(p => $"{p.Name,-42} {p.StateLabel}")
            .ToArray();

        await Dialogs.ShowMessageAsync(new MessageDialogViewModel
        {
            Title = "Review selection",
            Message = $"{selected.Length} package(s) selected. Nothing has been changed yet.",
            Causes =
            [
                "Each package is disabled for user 0 with pm disable-user.",
                "Disabled packages stay installed and can be re-enabled from this page.",
                $"{PackageCatalog.CoreProtected.Count + PackageCatalog.FireOsProtected.Count} protected packages are excluded and cannot be selected.",
            ],
            Detail = string.Join(System.Environment.NewLine, lines),
            Severity = MessageSeverity.Info,
        }).ConfigureAwait(true);
    }

    private async Task DisableSelectedAsync(CancellationToken cancellationToken)
    {
        if (State is not { } state)
        {
            return;
        }

        var names = Packages.Where(p => p.IsSelected).Select(p => p.Name).ToArray();
        var plan = Host.PackageActions.BuildDisablePlan(names, state, out var refused);

        if (refused.Count > 0)
        {
            await Dialogs.ShowInfoAsync(
                "Protected packages skipped",
                $"{refused.Count} package(s) are protected and were removed from the plan.",
                string.Join(System.Environment.NewLine, refused),
                MessageSeverity.Warning).ConfigureAwait(true);
        }

        await RunPlanAsync(plan, RefreshScope.Packages, cancellationToken).ConfigureAwait(true);
    }

    private async Task EnableSelectedAsync(CancellationToken cancellationToken)
    {
        if (State is not { } state)
        {
            return;
        }

        var names = Packages.Where(p => p.IsSelected).Select(p => p.Name).ToArray();
        var plan = Host.PackageActions.BuildEnablePlan(names, state);
        await RunPlanAsync(plan, RefreshScope.Packages, cancellationToken).ConfigureAwait(true);
    }
}
