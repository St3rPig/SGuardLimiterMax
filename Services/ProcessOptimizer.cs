using System.Diagnostics;
using SGuardLimiterMax.Models;

namespace SGuardLimiterMax.Services;

/// <summary>Known game with its process name and localised display name.</summary>
public record GameInfo(string ProcessName, string DisplayName);

/// <summary>
/// Applies CPU affinity and priority tweaks to SGuard and game processes.
/// </summary>
public static class ProcessOptimizer
{
    private static readonly string[] SGuardProcesses =
    [
        "SGuard64",
        "SGuardSvc64",
    ];

    /// <summary>Built-in game targets that are always monitored.</summary>
    private static readonly GameInfo[] BuiltInTargets =
    [
        new("VALORANT-Win64-Shipping",         "无畏契约"),
        new("DeltaForceClient-Win64-Shipping", "三角洲行动"),
    ];

    /// <summary>Runtime copy of the user's custom game list.</summary>
    private static List<CustomGameEntry> _customGames = [];

    /// <summary>
    /// Replaces the in-memory custom game list. Call this after loading or
    /// modifying <see cref="AppConfig.CustomGames"/>.
    /// </summary>
    public static void UpdateCustomGames(IEnumerable<CustomGameEntry> games)
        => _customGames = games.Where(g => !string.IsNullOrWhiteSpace(g.ProcessName)).ToList();

    private static nint LastCoreAffinity()
    {
        int coreCount = Environment.ProcessorCount;
        return (nint)(1L << (coreCount - 1));
    }

    /// <summary>
    /// Returns all monitored game processes (built-in + custom) that are currently running.
    /// Each entry is unique per process name.
    /// </summary>
    public static List<GameInfo> GetRunningGames()
    {
        var result = new List<GameInfo>();

        foreach (var game in BuiltInTargets)
        {
            var procs = SafeGetProcessesByName(game.ProcessName);
            if (procs.Length > 0)
            {
                foreach (var p in procs) p.Dispose();
                result.Add(game);
            }
        }

        foreach (var cg in _customGames)
        {
            var procs = SafeGetProcessesByName(cg.ProcessName);
            if (procs.Length > 0)
            {
                foreach (var p in procs) p.Dispose();
                result.Add(new GameInfo(cg.ProcessName,
                    string.IsNullOrWhiteSpace(cg.DisplayName) ? cg.ProcessName : cg.DisplayName));
            }
        }

        return result;
    }

    /// <summary>
    /// Throttles all running SGuard processes:
    ///   Priority → Idle,  Affinity → last CPU core only.
    /// </summary>
    public static void ThrottleSGuard()
    {
        nint lastCore = LastCoreAffinity();
        foreach (string name in SGuardProcesses)
        {
            foreach (var proc in SafeGetProcessesByName(name))
            {
                ApplySafe(proc, () =>
                {
                    proc.PriorityClass     = ProcessPriorityClass.Idle;
                    proc.ProcessorAffinity = lastCore;
                });
            }
        }
    }

    /// <summary>
    /// Applies scheduling changes to all running game processes.
    /// Built-in games use the global <paramref name="boostPriority"/> /
    /// <paramref name="unbindCpu0"/> flags. Custom games use their own per-entry flags.
    /// </summary>
    public static void ApplyGameSettings(bool boostPriority, bool unbindCpu0)
    {
        int  coreCount = Environment.ProcessorCount;
        nint allCores  = (nint)((1L << coreCount) - 1);

        // Built-in games — use global flags
        if (boostPriority || unbindCpu0)
        {
            nint builtInMask = unbindCpu0 ? (allCores & ~(nint)1) : allCores;
            foreach (var game in BuiltInTargets)
            {
                foreach (var proc in SafeGetProcessesByName(game.ProcessName))
                {
                    ApplySafe(proc, () =>
                    {
                        if (boostPriority) proc.PriorityClass     = ProcessPriorityClass.High;
                        if (unbindCpu0)    proc.ProcessorAffinity = builtInMask;
                    });
                }
            }
        }

        // Custom games — per-entry flags
        foreach (var cg in _customGames)
        {
            if (!cg.BoostPriority && !cg.UnbindCpu0) continue;
            nint customMask = cg.UnbindCpu0 ? (allCores & ~(nint)1) : allCores;
            foreach (var proc in SafeGetProcessesByName(cg.ProcessName))
            {
                ApplySafe(proc, () =>
                {
                    if (cg.BoostPriority) proc.PriorityClass     = ProcessPriorityClass.High;
                    if (cg.UnbindCpu0)    proc.ProcessorAffinity = customMask;
                });
            }
        }
    }

    private static Process[] SafeGetProcessesByName(string name)
    {
        try   { return Process.GetProcessesByName(name); }
        catch { return []; }
    }

    private static void ApplySafe(Process proc, Action action)
    {
        try   { action(); }
        catch { }
        finally { proc.Dispose(); }
    }
}
