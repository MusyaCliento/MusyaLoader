using Avalonia.Controls;

namespace SS14.Launcher.Views;

public partial class ProxyUnavailableDialog : Window
{
    public ProxyUnavailableDialog()
    {
        InitializeComponent();
    }

    public ProxyUnavailableDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    private void OkClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    private void SettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }
}
