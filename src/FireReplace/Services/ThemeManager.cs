using System.Windows;
using FireReplace.Core.Configuration;

namespace FireReplace.Services;

/// <summary>
/// Swaps the palette dictionary at runtime. Only the palette is replaced; the control styles use
/// <c>DynamicResource</c> so nothing has to be rebuilt.
/// </summary>
public static class ThemeManager
{
    /// <summary>Applies a theme to the running application.</summary>
    public static void Apply(AppTheme theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        var source = theme switch
        {
            AppTheme.Midnight => "Themes/Midnight.xaml",
            _ => "Themes/Dark.xaml",
        };

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        if (dictionaries.Count == 0)
        {
            return;
        }

        dictionaries[0] = new ResourceDictionary
        {
            Source = new Uri(source, UriKind.Relative),
        };
    }
}
