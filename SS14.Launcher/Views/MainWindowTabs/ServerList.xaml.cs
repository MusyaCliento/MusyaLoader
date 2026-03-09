using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Metadata;
using Serilog;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public sealed partial class ServerList : TemplatedControl
{
    public static readonly DirectProperty<ServerList, bool> ShowRoundTimeColumnProperty =
        AvaloniaProperty.RegisterDirect<ServerList, bool>(
            nameof(ShowRoundTimeColumn),
            o => o.ShowRoundTimeColumn,
            (o, v) => o.ShowRoundTimeColumn = v
        );

    private bool _showRoundTimeColumn = true;

    public bool ShowRoundTimeColumn
    {
        get => _showRoundTimeColumn;
        set => SetAndRaise(ShowRoundTimeColumnProperty, ref _showRoundTimeColumn, value);
    }

    public static readonly DirectProperty<ServerList, bool> ShowPlayerCountColumnProperty =
        AvaloniaProperty.RegisterDirect<ServerList, bool>(
            nameof(ShowPlayerCountColumn),
            o => o.ShowPlayerCountColumn,
            (o, v) => o.ShowPlayerCountColumn = v
        );

    private bool _showPlayerCountColumn = true;

    public bool ShowPlayerCountColumn
    {
        get => _showPlayerCountColumn;
        set => SetAndRaise(ShowPlayerCountColumnProperty, ref _showPlayerCountColumn, value);
    }

    public static readonly DirectProperty<ServerList, bool> ShowMapColumnProperty =
        AvaloniaProperty.RegisterDirect<ServerList, bool>(
            nameof(ShowMapColumn),
            o => o.ShowMapColumn,
            (o, v) => o.ShowMapColumn = v
        );

    private bool _showMapColumn = true;

    public bool ShowMapColumn
    {
        get => _showMapColumn;
        set => SetAndRaise(ShowMapColumnProperty, ref _showMapColumn, value);
    }

    public static readonly DirectProperty<ServerList, bool> ShowModeColumnProperty =
        AvaloniaProperty.RegisterDirect<ServerList, bool>(
            nameof(ShowModeColumn),
            o => o.ShowModeColumn,
            (o, v) => o.ShowModeColumn = v
        );

    private bool _showModeColumn = true;

    public bool ShowModeColumn
    {
        get => _showModeColumn;
        set => SetAndRaise(ShowModeColumnProperty, ref _showModeColumn, value);
    }

    public static readonly DirectProperty<ServerList, bool> ShowPingColumnProperty =
        AvaloniaProperty.RegisterDirect<ServerList, bool>(
            nameof(ShowPingColumn),
            o => o.ShowPingColumn,
            (o, v) => o.ShowPingColumn = v
        );

    private bool _showPingColumn = true;

    public bool ShowPingColumn
    {
        get => _showPingColumn;
        set => SetAndRaise(ShowPingColumnProperty, ref _showPingColumn, value);
    }

    public static readonly DirectProperty<ServerList, bool> ShowHeaderProperty =
        AvaloniaProperty.RegisterDirect<ServerList, bool>(
            nameof(ShowHeader),
            o => o.ShowHeader,
            (o, v) => o.ShowHeader = v
        );

    private bool _showHeader;

    public bool ShowHeader
    {
        get => _showHeader;
        set => SetAndRaise(ShowHeaderProperty, ref _showHeader, value);
    }

    public static readonly DirectProperty<ServerList, string?> ListTextProperty =
        AvaloniaProperty.RegisterDirect<ServerList, string?>(
            nameof(ListText),
            o => o.ListText,
            (o, v) => o.ListText = v
        );

    private string? _listText;

    /// <summary>
    /// Optional text which will be displayed in the server list area.
    /// If null or empty no text will be added.
    /// </summary>
    public string? ListText
    {
        get => _listText;
        set => SetAndRaise(ListTextProperty, ref _listText, value);
    }

    public static readonly DirectProperty<ServerList, bool> SpinnerVisibleProperty =
        AvaloniaProperty.RegisterDirect<ServerList, bool>(
            nameof(SpinnerVisible),
            o => o.SpinnerVisible,
            (o, v) => o.SpinnerVisible = v
        );

    private bool _spinnerVisible;

    public bool SpinnerVisible
    {
        get => _spinnerVisible;
        set => SetAndRaise(SpinnerVisibleProperty, ref _spinnerVisible, value);
    }

    public static readonly DirectProperty<ServerList, IReadOnlyCollection<ServerEntryViewModel>> ListProperty =
        AvaloniaProperty.RegisterDirect<ServerList, IReadOnlyCollection<ServerEntryViewModel>>(
            nameof(List),
            o => o.List,
            (o, v) => o.List = v
        );

    private IReadOnlyCollection<ServerEntryViewModel> _serverList = Array.Empty<ServerEntryViewModel>();

    public IReadOnlyCollection<ServerEntryViewModel> List
    {
        get => _serverList;
        set => SetAndRaise(ListProperty, ref _serverList, value);
    }

    public static readonly StyledProperty<object?> ContentProperty =
        ContentControl.ContentProperty.AddOwner<ServerList>();

    /// <summary>
    /// If an optional content block is provided it will be
    /// shown at the bottom of the server list.
    /// </summary>
    [Content]
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }
}
