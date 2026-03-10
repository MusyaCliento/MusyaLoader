using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Serilog;
using SS14.Launcher;
using SS14.Launcher.ViewModels;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class HomePageView : UserControl
{
    private HomePageViewModel? _viewModel;

    public HomePageView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Control = null;
        }

        _viewModel = DataContext as HomePageViewModel;

        if (_viewModel != null)
        {
            _viewModel.Control = this;
        }

        base.OnDataContextChanged(e);
    }

    private async void OpenReplayClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.MainWindowViewModel is not { } mainVm)
            return;

        if (this.GetVisualRoot() is not Window window)
        {
            Log.Error("Visual root isn't a window!");
            return;
        }

        var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select replay or content bundle file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Replay or content bundle files")
                {
                    Patterns = ["*.zip"],
                    MimeTypes = ["application/zip"],
                    AppleUniformTypeIdentifiers = ["zip"]
                }
            ]
        });

        if (result.Count == 0) // Cancelled
            return;

        using var file = result[0];
        if (!mainVm.IsContentBundleDropValid(file))
        {
            ReportInvalidContentBundle();
            return;
        }

        ConnectingViewModel.StartContentBundle(mainVm, file);
    }

    private static void ReportInvalidContentBundle()
    {
        const string message = "That file isn't a valid content bundle or replay.";
        const string caption = "Invalid Bundle";

        if (OperatingSystem.IsWindows())
        {
            Helpers.MessageBoxHelper(message, caption, 0);
            return;
        }

        Log.Warning("{Message}", message);
    }
}
