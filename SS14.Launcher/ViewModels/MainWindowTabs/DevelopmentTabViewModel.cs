using System;
using System.ComponentModel;
using ReactiveUI;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public sealed class DevelopmentTabViewModel : MainWindowTabViewModel
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    public DataManager Cfg { get; }

    public DevelopmentTabViewModel()
    {
        Cfg = Locator.Current.GetRequiredService<DataManager>();

        var entry = Cfg.GetCVarEntry(CVars.EngineOverrideEnabled);
        var weakThis = new WeakReference<DevelopmentTabViewModel>(this);
        PropertyChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (!weakThis.TryGetTarget(out var target))
            {
                entry.PropertyChanged -= handler;
                return;
            }

            if (args.PropertyName == nameof(ICVarEntry<bool>.Value))
                target.RaisePropertyChanged(nameof(Name));
        };
        entry.PropertyChanged += handler;
    }

    public override string Name => Cfg.GetCVar(CVars.EngineOverrideEnabled)
        ? _loc.GetString("tab-development-title-override")
        : _loc.GetString("tab-development-title");

    public bool DisableSigning
    {
        get => Cfg.GetCVar(CVars.DisableSigning);
        set
        {
            Cfg.SetCVar(CVars.DisableSigning, value);
            Cfg.CommitConfig();
        }
    }

    public bool EngineOverrideEnabled
    {
        get => Cfg.GetCVar(CVars.EngineOverrideEnabled);
        set
        {
            Cfg.SetCVar(CVars.EngineOverrideEnabled, value);
            Cfg.CommitConfig();
        }
    }

    public string EngineOverridePath
    {
        get => Cfg.GetCVar(CVars.EngineOverridePath);
        set
        {
            Cfg.SetCVar(CVars.EngineOverridePath, value);
            Cfg.CommitConfig();
        }
    }
}
