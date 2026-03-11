namespace SS14.Launcher.Utility;

public static class LauncherProxyRuntimeState
{
    public static bool DisableLauncherProxyForSession { get; set; }
    public static bool DisableUpdateProxyForSession { get; set; }
    public static bool DisableBypassProxyForSession { get; set; }
    public static string? UnavailableProxyMessage { get; set; }
    public static bool UnavailableProxyDialogShown { get; set; }

    public static void ResetDynamicProxyState()
    {
        DisableLauncherProxyForSession = false;
        DisableUpdateProxyForSession = false;
        DisableBypassProxyForSession = false;
        UnavailableProxyMessage = null;
        UnavailableProxyDialogShown = false;
    }
}
