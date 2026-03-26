using System.Diagnostics;

namespace SGuardLimiterMax.Services;

/// <summary>
/// Manages Windows power plans via powercfg.exe.
/// All operations are fire-and-forget with no visible console window.
/// </summary>
public static class PowerManager
{
    // Power plan GUIDs
    private const string GuidUltimatePerformance = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private const string GuidHighPerformance     = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string GuidBalanced            = "381b4222-f694-41f0-9685-ff5bb260df2e";

    /// <summary>
    /// Activates the best available performance plan.
    /// Strategy: try Ultimate Performance first, fall back to High Performance.
    /// </summary>
    public static void ActivatePerformancePlan()
    {
        if (!TrySetPlan(GuidUltimatePerformance))
            TrySetPlan(GuidHighPerformance);
    }

    /// <summary>
    /// Restores the Balanced power plan (called on game exit).
    /// </summary>
    public static void RestoreBalancedPlan()
    {
        TrySetPlan(GuidBalanced);
    }

    /// <summary>
    /// Runs ipconfig /flushdns silently.
    /// </summary>
    public static void FlushDns()
    {
        RunSilent("ipconfig", "/flushdns");
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempts to activate a power plan by GUID.
    /// Returns true on success (exit code 0), false otherwise.
    /// </summary>
    private static bool TrySetPlan(string guid)
    {
        using var process = RunSilent("powercfg", $"/setactive {guid}");
        process?.WaitForExit(3000);
        return process?.ExitCode == 0;
    }

    /// <summary>
    /// Starts a process with no visible window or shell.
    /// </summary>
    private static Process? RunSilent(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = fileName,
                Arguments              = arguments,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = false,
                RedirectStandardError  = false,
                WindowStyle            = ProcessWindowStyle.Hidden,
            };
            return Process.Start(psi);
        }
        catch
        {
            return null;
        }
    }
}
