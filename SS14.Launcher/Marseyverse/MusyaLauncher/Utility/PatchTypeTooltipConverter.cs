using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using SS14.Launcher.Localization;
using SS14.Launcher.Utility;
using Marsey.Patches;
using Marsey.Game.Resources;

namespace SS14.Launcher.Converters
{
    public class PatchTypeTooltipConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            var locMgr = LocalizationManager.Instance;
            string typeStr = string.Empty;
            string desc = string.Empty;

            // Determine patch type and get description
            if (value is IPatch patch)
            {
                typeStr = GetPatchTypeString(patch, locMgr);
                desc = patch.Desc ?? string.Empty;
                
                // Get .NET version
                string dotNetVersion = PatchInfoExtractor.GetPatchDotNetVersion(patch);
                
                // Format tooltip with .NET version
                return FormatTooltip(typeStr, dotNetVersion, desc, locMgr);
            }
            else if (value is ResourcePack resourcePack)
            {
                typeStr = locMgr.GetString("marsey-resourcepack-type");
                desc = resourcePack.Desc ?? string.Empty;
                
                return FormatTooltip(typeStr, null, desc, locMgr);
            }

            return string.Empty;
        }

        private string GetPatchTypeString(IPatch patch, LocalizationManager locMgr)
        {
            return patch switch
            {
                MarseyPatch => locMgr.GetString("marsey-patch-type-marsey"),
                _ => locMgr.GetString("marsey-patch-type-subverter")
            };
        }

        private string FormatTooltip(string typeStr, string? dotNetVersion, string desc, LocalizationManager locMgr)
        {
            var lines = new List<string>();
            
            // Add type and .NET version
            if (dotNetVersion != null && dotNetVersion != "Unknown")
            {
                lines.Add($"{typeStr}");
                lines.Add($"NET {dotNetVersion}");
            }
            else if (dotNetVersion == "Unknown")
            {
                lines.Add($"{typeStr}");
                lines.Add("NET Unknown");
            }
            else
            {
                lines.Add(typeStr);
            }
            
            // Add description if present
            if (!string.IsNullOrEmpty(desc))
            {
                lines.Add(string.Empty); // Empty line for spacing
                lines.Add(locMgr.GetString("marsey-patch-description") + ":");
                lines.Add(desc);
            }

            return string.Join("\n", lines).TrimEnd();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
