using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class ProxyTabView : UserControl
{
    public ProxyTabView()
    {
        InitializeComponent();
    }

    private async void AddProfileClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProxyTabViewModel vm)
            return;

        var draft = vm.CreateDefaultDraft();
        var dialog = new ProxyProfileDialog(draft, isEdit: false);
        var owner = this.GetVisualRoot() as Window;
        var result = owner != null ? await dialog.ShowDialog<ProxyTabViewModel.ProxyProfileDraft?>(owner) : null;
        if (result != null)
            vm.UpsertProfile(result);
    }

    private async void EditProfileClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProxyTabViewModel vm)
            return;

        var draft = vm.CreateDraftFromSelected();
        if (draft == null)
            return;

        var dialog = new ProxyProfileDialog(draft, isEdit: true);
        var owner = this.GetVisualRoot() as Window;
        var result = owner != null ? await dialog.ShowDialog<ProxyTabViewModel.ProxyProfileDraft?>(owner) : null;
        if (result != null)
            vm.UpsertProfile(result);
    }
}
