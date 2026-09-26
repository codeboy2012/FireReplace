using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FireReplace.Mvvm;

/// <summary>
/// Minimal <see cref="INotifyPropertyChanged"/> base class. FireReplace hand-rolls this instead of
/// taking an MVVM framework dependency: it keeps the assembly small and startup fast.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises <see cref="PropertyChanged"/> for the calling property.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Assigns <paramref name="value"/> to <paramref name="field"/> and raises a change
    /// notification when the value actually differs.
    /// </summary>
    /// <returns>True when the value changed.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Raises change notifications for several properties at once.</summary>
    protected void OnPropertiesChanged(params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            OnPropertyChanged(name);
        }
    }
}
