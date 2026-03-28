using System.Diagnostics;
using Microsoft.Win32;

namespace SGuardLimiterMax.Services;

/// <summary>
/// Manages the Windows auto-start entry for this application.
/// Uses Task Scheduler (via schtasks.exe) instead of the HKCU Run registry key,
/// because apps with requireAdministrator manifest are silently blocked by Windows
/// when launched from HKCU\Run — UAC is never prompted for Run-key entries.
/// </summary>
public static class StartupManager
{
    private const string TaskName    = "SGuardLimiterMax";
    private const string KeyName     = "SGuardLimiterMax";
    private const string RegistryRun = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Creates a Task Scheduler logon-trigger task that launches this exe
    /// with <c>--autostart</c> at the highest privilege level.
    /// Also removes any legacy HKCU Run entry left by older builds.
    /// </summary>
    public static void Enable()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return;

        CleanupLegacyRunKey();

        // /sc onlogon  — trigger when the current user logs on
        // /rl highest  — run with highest available privileges (required for this app)
        // /f           — force-overwrite if the task already exists (keeps path in sync)
        string args = $"/create /tn \"{TaskName}\" " +
                      $"/tr \"\\\"{exePath}\\\" --autostart\" " +
                      $"/sc onlogon /rl highest /f";
        RunSchtasks(args);
    }

    /// <summary>
    /// Deletes the Task Scheduler entry (and any legacy HKCU Run entry).
    /// </summary>
    public static void Disable()
    {
        CleanupLegacyRunKey();
        RunSchtasks($"/delete /tn \"{TaskName}\" /f");
    }

    /// <summary>
    /// Syncs the task with the current executable path.
    /// Called on every startup when <c>AutoStart</c> is true so the path
    /// stays correct if the user moves the application.
    /// </summary>
    public static void Sync(bool enable)
    {
        if (enable) Enable();
        else        Disable();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Launches schtasks.exe fire-and-forget (no blocking wait).
    /// Failures are swallowed — startup registration is non-fatal.
    /// </summary>
    private static void RunSchtasks(string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo("schtasks.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow  = true,
            })?.Dispose();
        }
        catch { }
    }

    /// <summary>
    /// Removes the HKCU Run key left by older versions of this app that used
    /// the registry approach (which doesn't work for elevated executables).
    /// </summary>
    private static void CleanupLegacyRunKey()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRun, writable: true);
            key?.DeleteValue(KeyName, throwOnMissingValue: false);
        }
        catch { }
    }
}
