using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SGuardLimiterMax.Services;

/// <summary>
/// A Windows power plan entry returned by powercfg /list.
/// </summary>
public record PowerPlanInfo(string Guid, string Name, bool IsActive);

/// <summary>
/// Manages Windows power plans via powercfg.exe.
/// All operations are fire-and-forget with no visible console window.
/// </summary>
public static class PowerManager
{
    private const string GuidUltimatePerformance = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private const string GuidHighPerformance     = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    private static string? _originalGuid;

    /// <summary>True if the power plan was switched and not yet restored.</summary>
    public static bool IsActivated => _originalGuid != null;

    /// <summary>
    /// Queries all power plans installed on the system via powercfg /list.
    /// Returns an empty list on failure.
    /// </summary>
    public static List<PowerPlanInfo> GetAllPlans()
    {
        var result = new List<PowerPlanInfo>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "powercfg",
                Arguments              = "/list",
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return result;
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            // Line format: "Power Scheme GUID: <guid>  (<name>) *"  (* = active)
            foreach (Match m in Regex.Matches(output,
                @"GUID:\s*([0-9a-fA-F\-]{36})\s+\(([^)]+)\)(\s+\*)?",
                RegexOptions.IgnoreCase))
            {
                string guid     = m.Groups[1].Value.Trim().ToLowerInvariant();
                string name     = m.Groups[2].Value.Trim();
                bool   isActive = m.Groups[3].Value.Trim() == "*";
                result.Add(new PowerPlanInfo(guid, name, isActive));
            }
        }
        catch { }
        return result;
    }

    /// <summary>
    /// Returns the currently active power plan, or null on failure.
    /// </summary>
    public static PowerPlanInfo? GetActivePlan()
    {
        var plans = GetAllPlans();
        return plans.FirstOrDefault(p => p.IsActive);
    }

    /// <summary>
    /// Captures the current active plan, then activates the best available
    /// performance plan. If <paramref name="targetGuid"/> is provided, that
    /// specific plan is used; otherwise falls back to Ultimate → High Performance.
    /// </summary>
    public static void ActivatePerformancePlan(string? targetGuid = null)
    {
        _originalGuid ??= GetActivePlanGuid();

        if (!string.IsNullOrWhiteSpace(targetGuid))
        {
            TrySetPlan(targetGuid);
            return;
        }

        if (!TrySetPlan(GuidUltimatePerformance))
            TrySetPlan(GuidHighPerformance);
    }

    /// <summary>
    /// Restores the power plan that was active before ActivatePerformancePlan was called.
    /// Falls back to Balanced if the original plan could not be captured.
    /// </summary>
    public static void RestoreOriginalPlan()
    {
        if (_originalGuid == null) return;
        TrySetPlan(_originalGuid);
        _originalGuid = null;
    }

    /// <summary>
    /// Discards the captured original plan without restoring it.
    /// Call this when the user explicitly chooses to keep the current plan on exit.
    /// </summary>
    public static void DiscardRestore() => _originalGuid = null;

    /// <summary>
    /// Runs ipconfig /flushdns silently.
    /// </summary>
    public static void FlushDns()
    {
        RunSilent("ipconfig", "/flushdns");
    }

    private static string? GetActivePlanGuid()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "powercfg",
                Arguments              = "/getactivescheme",
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return null;
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);
            var match = Regex.Match(output,
                @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return match.Success ? match.Value.ToLowerInvariant() : null;
        }
        catch { return null; }
    }

    private static bool TrySetPlan(string guid)
    {
        using var process = RunSilent("powercfg", $"/setactive {guid}");
        process?.WaitForExit(3000);
        return process?.ExitCode == 0;
    }

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
