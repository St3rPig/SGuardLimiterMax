using Microsoft.Win32;

namespace SGuardLimiterMax.Services;

/// <summary>
/// Manages the Windows startup registry entry for auto-launch.
/// Writes to HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// </summary>
public static class StartupManager
{
    private const string KeyName     = "SGuardLimiterMax";
    private const string RegistryRun = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Registers the current executable to launch on Windows startup with --autostart flag.
    /// </summary>
    public static void Enable()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRun, writable: true);
            key?.SetValue(KeyName, $"\"{exePath}\" --autostart");
        }
        catch { /* Registry write failure is non-fatal */ }
    }

    /// <summary>
    /// Syncs the registry entry with the current executable path.
    /// Call this on every startup when <c>AutoStart</c> is true so that the
    /// path stays correct if the user moves the application.
    /// </summary>
    public static void Sync(bool enable)
    {
        if (enable) Enable();
        else        Disable();
    }

    /// <summary>
    /// Removes the startup registry entry if it exists.
    /// </summary>
    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRun, writable: true);
            key?.DeleteValue(KeyName, throwOnMissingValue: false);
        }
        catch { /* Registry write failure is non-fatal */ }
    }
}
