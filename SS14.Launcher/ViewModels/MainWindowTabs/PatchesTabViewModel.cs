using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Avalonia.Data.Converters;
using Marsey.Config;
using Marsey.Game.Resources;
using Marsey.Misc;
using Marsey.Patches;
using Marsey.Subversion;
using Microsoft.Toolkit.Mvvm.Input;
using Serilog;
using SS14.Launcher.Localization;
using SS14.Launcher.Marseyverse;
using SS14.Launcher.Marseyverse.Engines;

namespace SS14.Launcher.ViewModels.MainWindowTabs
{
    public class PatchesTabViewModel : MainWindowTabViewModel
    {
        public override string Name => _loc.GetString("marsey-Patches-Tab");

        public ObservableCollection<MarseyPatch> MarseyPatches { get; } = new();
        public ObservableCollection<SubverterPatch> SubverterPatches { get; } = new();
        public ObservableCollection<ResourcePack> ResourcePacks { get; } = new();
        public ObservableCollection<CustomEngineInfo> CustomEngines { get; } = new();

        public ObservableCollection<IPatch> PatchesEnabled { get; } = new();
        public ObservableCollection<IPatch> PatchesDisabled { get; } = new();

        public ObservableCollection<SubverterPatch> SubverterPatchesEnabled { get; } = new();
        public ObservableCollection<SubverterPatch> SubverterPatchesDisabled { get; } = new();
        public ObservableCollection<ResourcePack> ResourcePacksEnabled { get; } = new();
        public ObservableCollection<ResourcePack> ResourcePacksDisabled { get; } = new();
        public ObservableCollection<CustomEngineInfo> CustomEnginesEnabled { get; } = new();
        public ObservableCollection<CustomEngineInfo> CustomEnginesDisabled { get; } = new();

        public ICommand OpenPatchDirectoryCommand { get; }
        public ICommand ReloadModsCommand { get; }
        public ICommand EnableRefreshCommand { get; }
        public ICommand SelectEngineCommand { get; }
        public ICommand MoveResourcePackUpCommand { get; }
        public ICommand MoveResourcePackDownCommand { get; }

        private readonly LocalizationManager _loc;

        public bool ShowRPacks => true;

        public PatchesTabViewModel()
        {
            _loc = LocalizationManager.Instance;
            OpenPatchDirectoryCommand = new RelayCommand(() => OpenPatchDirectory(MarseyVars.MarseyFolder));
            ReloadModsCommand = new RelayCommand(ReloadMods);
            EnableRefreshCommand = new RelayCommand(Refresh);
            SelectEngineCommand = new RelayCommand<CustomEngineInfo?>(SelectEngine);
            MoveResourcePackUpCommand = new RelayCommand<ResourcePack?>(pack => MoveResourcePack(pack, -1));
            MoveResourcePackDownCommand = new RelayCommand<ResourcePack?>(pack => MoveResourcePack(pack, 1));

            ReloadMods();
        }

        private void ReloadMods()
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "Mods", "========== MOD RECHECK START ==========");
            LoadInitialResources();
            LoadPatches();
            EnableConfiguredPatches();
            UpdateFilteredCollections();
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "Mods", "========== MOD RECHECK END ==========");
        }

        private void LoadInitialResources()
        {
            FileHandler.LoadAssemblies();
            ResMan.LoadDir();
        }

        private void LoadPatches()
        {
            MarseyPatches.Clear();
            SubverterPatches.Clear();
            ResourcePacks.Clear();
            CustomEngines.Clear();

            LoadPatchList(Marsyfier.GetMarseyPatches(), MarseyPatches, "marseypatches");
            LoadPatchList(Subverter.GetSubverterPatches(), SubverterPatches, "subverterpatches");
            LoadResPacks(ResMan.GetRPacks(), ResourcePacks);
            LoadCustomEngines();

            UpdateFilteredCollections();
        }

        private void EnableConfiguredPatches()
        {
            List<string> assemblies = Persist.LoadPatchlistConfig();
            LoadEnabledPatches(assemblies, MarseyPatches);
            LoadEnabledPatches(assemblies, SubverterPatches);

            var resourcePackConfig = Persist.LoadResourcePacksConfig();
            if (resourcePackConfig.Count == 0)
                return;

            var byDir = ResourcePacks.ToDictionary(p => p.Dir, p => p, StringComparer.OrdinalIgnoreCase);
            var ordered = new List<ResourcePack>();

            foreach (var entry in resourcePackConfig)
            {
                if (!byDir.TryGetValue(entry.Dir, out var pack))
                    continue;

                pack.Enabled = entry.Enabled;
                ordered.Add(pack);
                byDir.Remove(entry.Dir);
            }

            foreach (var pack in byDir.Values)
            {
                pack.Enabled = true;
                ordered.Add(pack);
            }

            ResourcePacks.Clear();
            foreach (var pack in ordered)
            {
                ResourcePacks.Add(pack);
            }
        }

        private void OpenPatchDirectory(string directoryName)
        {
            try
            {
                var path = string.IsNullOrWhiteSpace(directoryName)
                    ? Directory.GetCurrentDirectory()
                    : Path.Combine(Directory.GetCurrentDirectory(), directoryName);

                if (!Directory.Exists(path) && File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = path });
                    return;
                }

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = path });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open patch directory");
            }
        }

        private void LoadPatchList<T>(List<T> patches, ICollection<T> patchList, string patchName) where T : IPatch
        {
            foreach (var patch in patches.Where(patch => !patchList.Any(r => r.Equals(patch))))
            {
                patchList.Add(patch);
            }

            Log.Debug("Refreshed {PatchName}, got {Count}.", patchName, patchList.Count);
        }

        private void LoadResPacks(List<ResourcePack> resPacks, ICollection<ResourcePack> rPacks)
        {
            foreach (var resource in resPacks)
            {
                if (rPacks.All(r => r.Dir != resource.Dir))
                {
                    rPacks.Add(resource);
                }
            }

            Log.Debug("Refreshed {PatchName}, got {Count}.", "resourcepacks", rPacks.Count);
        }

        private void Refresh()
        {
            List<string> assemblyFileNames = new();
            SaveEnabledPatches(MarseyPatches, assemblyFileNames);
            SaveEnabledPatches(SubverterPatches, assemblyFileNames);

            Log.Debug("Saved {Count} patches to config", assemblyFileNames.Count);
            Persist.SavePatchlistConfig(assemblyFileNames);

            SaveResourcePacksConfig();
            UpdateFilteredCollections();
        }

        private void UpdateFilteredCollections()
        {
            PatchesEnabled.Clear();
            PatchesDisabled.Clear();

            SubverterPatchesEnabled.Clear();
            SubverterPatchesDisabled.Clear();
            ResourcePacksEnabled.Clear();
            ResourcePacksDisabled.Clear();
            CustomEnginesEnabled.Clear();
            CustomEnginesDisabled.Clear();

            foreach (var patch in MarseyPatches.Cast<IPatch>().Concat(SubverterPatches))
            {
                if (patch.Enabled)
                    PatchesEnabled.Add(patch);
                else
                    PatchesDisabled.Add(patch);
            }

            foreach (var patch in SubverterPatches)
            {
                if (patch.Enabled)
                    SubverterPatchesEnabled.Add(patch);
                else
                    SubverterPatchesDisabled.Add(patch);
            }

            foreach (var pack in ResourcePacks)
            {
                if (pack.Enabled)
                    ResourcePacksEnabled.Add(pack);
                else
                    ResourcePacksDisabled.Add(pack);
            }

            foreach (var engine in CustomEngines)
            {
                if (engine.Enabled)
                    CustomEnginesEnabled.Add(engine);
                else
                    CustomEnginesDisabled.Add(engine);
            }
        }

        private void LoadCustomEngines()
        {
            CustomEngines.Clear();

            var engines = CustomEngineRegistry.ScanEngines();
            var selected = CustomEngineRegistry.LoadSelection();

            foreach (var engine in engines)
            {
                if (!string.IsNullOrWhiteSpace(selected) &&
                    string.Equals(engine.SourcePath, selected, StringComparison.OrdinalIgnoreCase))
                {
                    engine.Enabled = true;
                }

                CustomEngines.Add(engine);
            }

            Log.Debug("Loaded {Count} custom engine(s).", CustomEngines.Count);
            MarseyLogger.Log(MarseyLogger.LogType.INFO, "Engines", $"Loaded {CustomEngines.Count} custom engine(s).");
        }

        private void SelectEngine(CustomEngineInfo? engine)
        {
            if (engine == null)
                return;

            if (!engine.Enabled)
            {
                CustomEngineRegistry.SaveSelection(null);
                UpdateFilteredCollections();
                return;
            }

            if (!engine.CanUse)
            {
                engine.Enabled = false;
                UpdateFilteredCollections();
                return;
            }

            foreach (var entry in CustomEngines)
                entry.Enabled = false;

            engine.Enabled = true;
            CustomEngineRegistry.SaveSelection(engine.SourcePath);
            UpdateFilteredCollections();
        }

        private void SaveResourcePacksConfig()
        {
            var config = ResourcePacks
                .Select(p => new Persist.ResourcePackConfigEntry(p.Dir, p.Enabled))
                .ToList();
            Persist.SaveResourcePacksConfig(config);
            Log.Debug("Saved {Count} resource packs to config", config.Count);
        }

        private void MoveResourcePack(ResourcePack? pack, int delta)
        {
            if (pack == null || delta == 0)
                return;

            int index = ResourcePacks.IndexOf(pack);
            if (index < 0)
                return;

            int newIndex = index + delta;
            if (newIndex < 0 || newIndex >= ResourcePacks.Count)
                return;

            ResourcePacks.Move(index, newIndex);
            UpdateFilteredCollections();
            SaveResourcePacksConfig();
        }

        private static void SaveEnabledPatches(IEnumerable<IPatch> patches, List<string> fileNames)
        {
            foreach (var patch in patches)
            {
                if (patch.Enabled)
                {
                    fileNames.Add(Path.GetFileName(patch.Asmpath));
                }
            }
        }

        private static void LoadEnabledPatches(List<string> fileNames, IEnumerable<IPatch> patches)
        {
            foreach (var patch in from filename in fileNames from patch in patches where Path.GetFileName(patch.Asmpath) == filename select patch)
            {
                patch.Enabled = true;
            }
        }
    }
}

public class BooleanToClassConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? "enabled-patch" : "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value as string) == "enabled-patch";
    }
}

public class PathToFileNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? path = value as string;
        return Path.GetFileName(path);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}

public class BooleanToPreloadConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "(preload)" : "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}
