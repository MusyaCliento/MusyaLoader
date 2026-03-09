using System;

namespace SS14.Launcher;

public static class LauncherVersion
{
    public const string Name = "Musyaloader";
    public static Version? Version => typeof(LauncherVersion).Assembly.GetName().Version;
}
