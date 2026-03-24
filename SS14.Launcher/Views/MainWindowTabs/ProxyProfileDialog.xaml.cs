using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SS14.Launcher.Localization;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class ProxyProfileDialog : Window
{
    private readonly string? _id;
    private readonly LocalizationManager _loc = LocalizationManager.Instance;

    public ProxyProfileDialog()
    {
        InitializeComponent();
    }

    public ProxyProfileDialog(ProxyTabViewModel.ProxyProfileDraft draft, bool isEdit)
    {
        InitializeComponent();

        _id = draft.Id;
        NameBox.Text = draft.Name;
        HostBox.Text = draft.Host;
        PortBox.Text = Math.Clamp(draft.Port, 1, 65535).ToString();
        UserBox.Text = draft.Username;
        PassHiddenBox.Text = draft.Password;
        PassVisibleBox.Text = draft.Password;
        SaveButton.Content = isEdit ? _loc.GetString("tab-proxy-dialog-save") : _loc.GetString("tab-proxy-add");
    }

    private void SaveClicked(object? sender, RoutedEventArgs e)
    {
        var name = (NameBox.Text ?? "").Trim();
        var host = (HostBox.Text ?? "").Trim();
        var nameResolved = string.IsNullOrWhiteSpace(name) ? host : name;
        var portRaw = (PortBox.Text ?? "").Trim();
        if (!int.TryParse(portRaw, out var parsedPort))
            parsedPort = 1080;
        var port = Math.Clamp(parsedPort, 1, 65535);

        if (string.IsNullOrWhiteSpace(host))
        {
            ErrorText.Text = _loc.GetString("tab-proxy-dialog-host-required");
            return;
        }

        var draft = new ProxyTabViewModel.ProxyProfileDraft(
            _id,
            nameResolved,
            host,
            port,
            UserBox.Text ?? "",
            GetPasswordText());

        Close(draft);
    }

    private void CancelClicked(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void ShowPasswordChanged(object? sender, RoutedEventArgs e)
    {
        var show = ShowPasswordBox.IsChecked == true;
        if (show)
            PassVisibleBox.Text = PassHiddenBox.Text;
        else
            PassHiddenBox.Text = PassVisibleBox.Text;

        PassVisibleBox.IsVisible = show;
        PassHiddenBox.IsVisible = !show;
    }

    private string GetPasswordText()
    {
        return ShowPasswordBox.IsChecked == true
            ? PassVisibleBox.Text ?? ""
            : PassHiddenBox.Text ?? "";
    }
}
