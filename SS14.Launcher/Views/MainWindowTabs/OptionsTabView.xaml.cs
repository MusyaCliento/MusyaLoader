using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Utility;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class OptionsTabView : UserControl
{
    public OptionsTabView()
    {
        InitializeComponent();

        Flip.Command = ReactiveCommand.Create(() =>
        {
            var window = (Window?) VisualRoot;
            if (window == null)
                return;

            window.Classes.Add("DoAFlip");

            DispatcherTimer.RunOnce(() => { window.Classes.Remove("DoAFlip"); }, TimeSpan.FromSeconds(1));
        });
    }

    public async void ClearEnginesPressed(object? _1, RoutedEventArgs _2)
    {
        ((OptionsTabViewModel)DataContext!).ClearEngines();
        await ClearEnginesButton.DisplayDoneMessage();
    }

    public async void ClearServerContentPressed(object? _1, RoutedEventArgs _2)
    {
        var blocked = !await ((OptionsTabViewModel)DataContext!).ClearServerContent();
        var locMgr = Locator.Current.GetService<LocalizationManager>()!;

        await ClearServerContentButton.DisplayDoneMessage(
            blocked ? locMgr.GetString("tab-options-clear-content-close-client") : null);
    }

    private async void OpenHubSettings(object? sender, RoutedEventArgs args)
    {
        await new HubSettingsDialog().ShowDialog((Window)this.GetVisualRoot()!);
    }

    private async void PickCustomFont(object? sender, RoutedEventArgs args)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null || DataContext is not OptionsTabViewModel vm)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select custom font",
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Font files")
                {
                    Patterns = new[] { "*.ttf", "*.otf", "*.ttc" }
                }
            }
        });

        if (files.Count == 0)
            return;

        var localPath = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
            return;

        vm.ApplyCustomFontFile(localPath);
    }

    private async void ExportCustomTheme(object? sender, RoutedEventArgs args)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null || DataContext is not OptionsTabViewModel vm)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export custom theme",
            SuggestedFileName = "custom-theme.json",
            DefaultExtension = "json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON files")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        if (file == null)
            return;

        var json = vm.ExportCustomThemeJson();
        var localPath = file.TryGetLocalPath();

        if (!string.IsNullOrWhiteSpace(localPath))
        {
            await File.WriteAllTextAsync(localPath, json);
        }
        else
        {
            await using var stream = await file.OpenWriteAsync();
            using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
            await writer.FlushAsync();
        }

        if (sender is Button button)
            await button.DisplayDoneMessage();
    }

    private async void ImportCustomTheme(object? sender, RoutedEventArgs args)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null || DataContext is not OptionsTabViewModel vm)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Import custom theme",
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON files")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        if (files.Count == 0)
            return;

        string json;
        var localPath = files[0].TryGetLocalPath();

        if (!string.IsNullOrWhiteSpace(localPath))
        {
            json = await File.ReadAllTextAsync(localPath);
        }
        else
        {
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            json = await reader.ReadToEndAsync();
        }

        var imported = vm.TryImportCustomThemeJson(json, out _);

        if (sender is Button button)
        {
            await button.DisplayDoneMessage(imported ? null : "Invalid theme preset file.");
        }
    }

    private async void ResetCustomTheme(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not OptionsTabViewModel vm)
            return;

        vm.ResetCustomTheme();

        if (sender is Button button)
            await button.DisplayDoneMessage();
    }
}
