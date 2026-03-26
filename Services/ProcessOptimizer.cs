using System.Diagnostics;

namespace SGuardLimiterMax.Services;

/// <summary>
/// Applies CPU affinity and priority tweaks to SGuard and game processes.
/// No WMI — uses direct System.Diagnostics.Process API only.
/// </summary>
public static class ProcessOptimizer
{
    // ── Target process names (without .exe extension) ────────────────────────

    private static readonly string[] SGuardProcesses =
    [
        "SGuard64",
        "SGuardSvc64",
    ];

    private static readonly string[] GameProcesses =
    [
        "VALORANT-Win64-Shipping",
        "DeltaForceClient-Win64-Shipping",
    ];

    // ── Affinity helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds a bitmask that targets only the last logical CPU core.
    /// e.g. on an 8-core system this returns 0b10000000 = 128.
    /// </summary>
    private static nint LastCoreAffinity()
    {
        int coreCount = Environment.ProcessorCount;
        return (nint)(1L << (coreCount - 1));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Throttles all running SGuard processes:
    ///   - Priority  → Idle
    ///   - Affinity  → last CPU core only
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
                    proc.PriorityClass       = ProcessPriorityClass.Idle;
                    proc.ProcessorAffinity   = lastCore;
                });
            }
        }
    }

    /// <summary>
    /// Boosts all running game processes:
    ///   - Priority  → High
    ///   - Affinity  → all cores (optionally excluding CPU 0)
    /// </summary>
    /// <param name="unbindCpu0">
    /// When true, clears the CPU 0 bit from the affinity mask (Affinity &amp;= ~1).
    /// </param>
    public static void BoostGameProcesses(bool unbindCpu0 = false)
    {
        int  coreCount  = Environment.ProcessorCount;
        nint allCores   = (nint)((1L << coreCount) - 1);  // all bits set
        nint targetMask = unbindCpu0 ? (allCores & ~(nint)1) : allCores;

        foreach (string name in GameProcesses)
        {
            foreach (var proc in SafeGetProcessesByName(name))
            {
                ApplySafe(proc, () =>
                {
                    proc.PriorityClass     = ProcessPriorityClass.High;
                    proc.ProcessorAffinity = targetMask;
                });
            }
        }
    }

    /// <summary>
    /// Returns true if at least one monitored game process is currently running.
    /// Used by the background polling loop — no WMI, no P/Invoke.
    /// </summary>
    public static bool IsAnyGameRunning()
    {
        foreach (string name in GameProcesses)
        {
            var procs = SafeGetProcessesByName(name);
            if (procs.Length > 0)
            {
                foreach (var p in procs) p.Dispose();
                return true;
            }
        }
        return false;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Process.GetProcessesByName that swallows access/platform exceptions.
    /// </summary>
    private static Process[] SafeGetProcessesByName(string name)
    {
        try   { return Process.GetProcessesByName(name); }
        catch { return []; }
    }

    /// <summary>
    /// Applies a delegate to a process and disposes it; swallows all exceptions
    /// (access denied, process already exited, etc.).
    /// </summary>
    private static void ApplySafe(Process proc, Action action)
    {
        try   { action(); }
        catch { /* insufficient privileges or process gone — skip */ }
        finally { proc.Dispose(); }
    }
}
