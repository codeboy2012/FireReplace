using System.Windows.Controls;

namespace FireReplace.Views;

/// <summary>The setup and connection screen.</summary>
public partial class ConnectView : UserControl
{
    /// <summary>Creates the view and focuses the address box.</summary>
    public ConnectView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            IpBox.Focus();
            IpBox.SelectAll();
        };
    }
}
