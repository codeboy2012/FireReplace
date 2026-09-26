using System.Collections.ObjectModel;
using FireReplace.Core.Catalog;
using FireReplace.Core.Discovery;
using FireReplace.Core.Models;
using FireReplace.Core.Services;
using FireReplace.Dialogs;
using FireReplace.Mvvm;
using FireReplace.Services;

namespace FireReplace.ViewModels;

/// <summary>Which packages the list shows.</summary>
public enum PackageFilter
{
    /// <summary>Everything installed.</summary>
    All,

    /// <summary>Only enabled packages.</summary>
    Enabled,

    /// <summary>Only disabled packages.</summary>
    Disabled,

    /// <summary>Only packages FireReplace protects.</summary>
    Protected,
}

/// <summary>One installable APK found in the configured APK folder.</summary>
public sealed class ApkRowViewModel : ObservableObject
{
    private string _installedState = "Unknown";

    /// <summary>Creates the row.</summary>
    internal ApkRowViewModel(ApkLocation location, LauncherApp? app, Func<ApkRowViewModel, CancellationToken, Task> install)
    {
        Location = location;
        App = app;
        InstallCommand = new AsyncRelayCommand(ct => install(this, ct), () => Location.Found);
    }

    /// <summary>Where the APK was found.</summary>
    public ApkLocation Location { get; }

    /// <summary>The matching launcher catalog entry, when known.</summary>
    public LauncherApp? App { get; }

    /// <summary>Display name.</summary>
    public string DisplayName => Location.DisplayName;

    /// <summary>Resolved file name, or the expected pattern when missing.</summary>
    public string FileName => Location.FileName ?? Location.FileNamePattern;

    /// <summary>True when the file exists.</summary>
    public bool Found => Location.Found;

    /// <summary>Tick or cross.</summary>
    public string Glyph => Found ? "\u2713" : "\u2717";

    /// <summary>Whether the package is currently installed on the device.</summary>
    public string InstalledState
    {
        get => _installedState;
        set => SetProperty(ref _installedState, value);
    }

    /// <summary>Installs the APK after confirmation.</summary>
    public AsyncRelayCommand InstallCommand { get; }
}

/// <summary>
/// Package management and APK installation.
/// </summary>
/// <remarks>
/// The list is built from the device's own <c>pm list packages</c> output. A package is never treated
/// as safe or unsafe because of how its name reads: only the curated catalog decides, and everything
/// outside it is shown as plain "Other".
/// </remarks>
public sealed class AppsViewModel : PageViewModel
{
    private readonly List<PackageRowViewModel> _all = [];
    private string _searchText = string.Empty;
    private PackageFilter _filter = PackageFilter.All;
    private string _resultSummary = "No device snapshot yet.";

    /// <summary>Creates the page.</summary>
    public AppsViewModel(AppHost host)
        : base(host)
    {
        RefreshCommand = new AsyncRelayCommand(ct => RefreshAsync(RefreshScope.Packages, ct));
        DisableCommand = new AsyncRelayCommand(
            (parameter, ct) => DisableAsync(parameter as PackageRowViewModel, ct));
        EnableCommand = new AsyncRelayCommand(
            (parameter, ct) => EnableAsync(parameter as PackageRowViewModel, ct));
        RescanApksCommand = new RelayCommand(RebuildApks);

        RebuildApks();
    }

    /// <inheritdoc />
    public override string Title => "Apps";

    /// <inheritdoc />
    public override string Subtitle =>
        "Search the packages installed on your Fire TV, and install the APKs found in the local apks folder.";

    /// <summary>Filtered package rows.</summary>
    public ObservableCollection<PackageRowViewModel> Results { get; } = [];

    /// <summary>APK rows.</summary>
    public ObservableCollection<ApkRowViewModel> Apks { get; } = [];

    /// <summary>Search text matched against the package name.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    /// <summary>Active filter.</summary>
    public PackageFilter Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
            {
                OnPropertiesChanged(
                    nameof(IsAllSelected),
                    nameof(IsEnabledSelected),
                    nameof(IsDisabledSelected),
                    nameof(IsProtectedSelected));
                ApplyFilter();
            }
        }
    }

    /// <summary>Filter binding helper.</summary>
    public bool IsAllSelected
    {
        get => Filter == PackageFilter.All;
        set { if (value) { Filter = PackageFilter.All; } }
    }

    /// <summary>Filter binding helper.</summary>
    public bool IsEnabledSelected
    {
        get => Filter == PackageFilter.Enabled;
        set { if (value) { Filter = PackageFilter.Enabled; } }
    }

    /// <summary>Filter binding helper.</summary>
    public bool IsDisabledSelected
    {
        get => Filter == PackageFilter.Disabled;
        set { if (value) { Filter = PackageFilter.Disabled; } }
    }

    /// <summary>Filter binding helper.</summary>
    public bool IsProtectedSelected
    {
        get => Filter == PackageFilter.Protected;
        set { if (value) { Filter = PackageFilter.Protected; } }
    }

    /// <summary>Summary such as "42 of 318 packages".</summary>
    public string ResultSummary
    {
        get => _resultSummary;
        private set => SetProperty(ref _resultSummary, value);
    }

    /// <summary>Re-reads the package lists.</summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>Disables the package passed as the command parameter.</summary>
    public AsyncRelayCommand DisableCommand { get; }

    /// <summary>Enables the package passed as the command parameter.</summary>
    public AsyncRelayCommand EnableCommand { get; }

    /// <summary>Re-scans the APK folder.</summary>
    public RelayCommand RescanApksCommand { get; }

    /// <inheritdoc />
    public override void OnDeviceStateChanged(DeviceState state)
    {
        base.OnDeviceStateChanged(state);

        _all.Clear();
        foreach (var name in state.InstalledPackages.OrderBy(n => n, StringComparer.Ordinal))
        {
            _all.Add(new PackageRowViewModel(state.PackageInfoFor(name)));
        }

        // Curated packages that are not installed are still worth showing under the Protected filter.
        foreach (var entry in PackageCatalog.All)
        {
            if (!state.InstalledPackages.Contains(entry.Name))
            {
                _all.Add(new PackageRowViewModel(state.PackageInfoFor(entry.Name)));
            }
        }

        foreach (var apk in Apks)
        {
            apk.InstalledState = apk.App is null
                ? "—"
                : state.StateOf(apk.App.PackageName) switch
                {
                    PackageState.Enabled => "Installed",
                    PackageState.Disabled => "Installed (disabled)",
                    PackageState.NotInstalled => "Not installed",
                    _ => "Unknown",
                };
        }

        ApplyFilter();
    }

    private void RebuildApks()
    {
        Host.RefreshToolchain();
        Apks.Clear();

        foreach (var location in Host.Apks)
        {
            Apks.Add(new ApkRowViewModel(location, LauncherCatalog.ByPackage(location.PackageName), InstallAsync));
        }
    }

    private void ApplyFilter()
    {
        var query = _searchText.Trim();

        IEnumerable<PackageRowViewModel> source = _filter switch
        {
            PackageFilter.Enabled => _all.Where(p => p.State == PackageState.Enabled),
            PackageFilter.Disabled => _all.Where(p => p.State == PackageState.Disabled),
            PackageFilter.Protected => _all.Where(p => p.IsGuarded),
            _ => _all.Where(p => p.IsInstalled),
        };

        if (query.Length > 0)
        {
            source = source.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = source.ToArray();

        Results.Clear();
        foreach (var row in filtered)
        {
            Results.Add(row);
        }

        var total = _all.Count(p => p.IsInstalled);
        ResultSummary = total == 0
            ? "No packages read yet. Press Refresh."
            : $"{filtered.Length} shown of {total} installed";
    }

    private async Task DisableAsync(PackageRowViewModel? row, CancellationToken cancellationToken)
    {
        if (row is null || State is not { } state)
        {
            return;
        }

        if (row.IsGuarded)
        {
            await Dialogs.ShowInfoAsync(
                "Protected package",
                $"{row.Name} is on FireReplace's protected list and will not be disabled.",
                row.Description,
                MessageSeverity.Warning).ConfigureAwait(true);
            return;
        }

        var plan = Host.PackageActions.BuildDisablePlan([row.Name], state, out _);
        await RunPlanAsync(plan, RefreshScope.Packages, cancellationToken).ConfigureAwait(true);
    }

    private async Task EnableAsync(PackageRowViewModel? row, CancellationToken cancellationToken)
    {
        if (row is null || State is not { } state)
        {
            return;
        }

        var plan = Host.PackageActions.BuildEnablePlan([row.Name], state);
        await RunPlanAsync(plan, RefreshScope.Packages, cancellationToken).ConfigureAwait(true);
    }

    private async Task InstallAsync(ApkRowViewModel row, CancellationToken cancellationToken)
    {
        if (State is not { } state || row.Location.Path is null)
        {
            return;
        }

        var app = row.App ?? new LauncherApp(
            row.Location.DisplayName,
            row.Location.DisplayName,
            row.Location.PackageName,
            row.Location.FileNamePattern,
            "APK found in the local apks folder.");

        var plan = Host.LauncherActions.BuildInstallPlan(app, row.Location, state);
        await RunPlanAsync(plan, RefreshScope.Packages, cancellationToken).ConfigureAwait(true);
    }
}
