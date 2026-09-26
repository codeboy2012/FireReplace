using FireReplace.Mvvm;

namespace FireReplace.ViewModels;

/// <summary>
/// The first-run safety checklist. It appears once, requires three explicit acknowledgements, and is
/// never shown again unless the user asks for it from Settings.
/// </summary>
public sealed class WelcomeViewModel : ObservableObject
{
    private bool _understandsModification;
    private bool _willReviewChanges;
    private bool _understandsProtection;

    /// <summary>Creates the view model.</summary>
    public WelcomeViewModel()
    {
        ContinueCommand = new RelayCommand(() => Completed?.Invoke(this, EventArgs.Empty), () => CanContinue);
    }

    /// <summary>Raised when the user has acknowledged everything and pressed Continue.</summary>
    public event EventHandler? Completed;

    /// <summary>"I understand FireReplace modifies my TV".</summary>
    public bool UnderstandsModification
    {
        get => _understandsModification;
        set { if (SetProperty(ref _understandsModification, value)) { Revalidate(); } }
    }

    /// <summary>"I will review changes before applying them".</summary>
    public bool WillReviewChanges
    {
        get => _willReviewChanges;
        set { if (SetProperty(ref _willReviewChanges, value)) { Revalidate(); } }
    }

    /// <summary>"I understand FireReplace does not bypass protected Fire OS packages".</summary>
    public bool UnderstandsProtection
    {
        get => _understandsProtection;
        set { if (SetProperty(ref _understandsProtection, value)) { Revalidate(); } }
    }

    /// <summary>True when all three boxes are ticked.</summary>
    public bool CanContinue => UnderstandsModification && WillReviewChanges && UnderstandsProtection;

    /// <summary>What FireReplace will never do, listed on the welcome screen.</summary>
    public IReadOnlyList<string> Guarantees { get; } =
    [
        "No root, no exploits, no bootloader unlocking, no firmware flashing",
        "No factory reset and no attempt to wipe your TV",
        "No bypassing packages that Fire OS protects",
        "No arbitrary shell command box — only the specific operations on these pages",
        "Every change is shown with its current and new value before it runs",
    ];

    /// <summary>Continues into the application.</summary>
    public RelayCommand ContinueCommand { get; }

    private void Revalidate()
    {
        OnPropertyChanged(nameof(CanContinue));
        ContinueCommand.RaiseCanExecuteChanged();
    }
}
