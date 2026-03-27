namespace SGuardLimiterMax.Models;

/// <summary>
/// Serializable configuration model. Stored as Config.json next to the executable.
/// </summary>
public class AppConfig
{
    /// <summary>Lower SGuard priority to Idle and restrict affinity to the last CPU core.</summary>
    public bool ThrottleSGuard { get; set; } = true;

    /// <summary>Elevate game process priority to High. Optional — effect varies by system.</summary>
    public bool BoostGamePriority { get; set; } = true;

    /// <summary>Remove CPU 0 from the game process affinity mask. Optional — may be negative on some CPUs.</summary>
    public bool UnbindCPU { get; set; } = false;

    /// <summary>Switch to a specific power plan while game is running. Null = auto-select best available.</summary>
    public bool OptimizePower { get; set; } = false;

    /// <summary>GUID of the power plan to activate during a game session. Null means auto-select (Ultimate → High Performance).</summary>
    public string? TargetPowerPlanGuid { get; set; } = null;

    /// <summary>Run ipconfig /flushdns when game session starts.</summary>
    public bool FlushDNS { get; set; } = true;

    /// <summary>Raise Windows timer resolution while game is running.</summary>
    public bool TimerResolution { get; set; } = false;

    /// <summary>
    /// Target timer resolution in 100-nanosecond units (e.g. 5000 = 0.5 ms, 10000 = 1 ms).
    /// 0 is a sentinel meaning "do not override — use system default."
    /// Applied when TimerResolution is true.
    /// </summary>
    public uint TimerResolutionPeriod100Ns { get; set; } = 10000;

    /// <summary>Automatically minimize to tray when a game is detected.</summary>
    public bool AutoMinimizeOnGame { get; set; } = false;

    /// <summary>Automatically shut down the app when all monitored games exit. Default is false (stay resident).</summary>
    public bool ExitWithGame { get; set; } = false;

    /// <summary>Register this executable to run on Windows startup via HKCU Run key.</summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>Show tray balloon notifications (game detected, game exited, minimized). Disable for silent operation.</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Restore the original power plan when the app exits or a game session ends. Default true.</summary>
    public bool RestorePowerOnExit { get; set; } = true;

    /// <summary>Restore the system timer resolution when the app exits or a game session ends. Default true.</summary>
    public bool RestoreTimerOnExit { get; set; } = true;

    /// <summary>User-defined game processes to monitor and optimize alongside the built-in list.</summary>
    public List<CustomGameEntry> CustomGames { get; set; } = [];
}

/// <summary>
/// A user-defined game process entry with per-game scheduling flags.
/// </summary>
public class CustomGameEntry
{
    /// <summary>Executable name without extension, e.g. "RustClient".</summary>
    public string ProcessName { get; set; } = "";

    /// <summary>Friendly display name shown in the UI and status bar.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Elevate this process to High priority when detected.</summary>
    public bool BoostPriority { get; set; } = true;

    /// <summary>Remove CPU 0 from affinity mask for this process.</summary>
    public bool UnbindCpu0 { get; set; } = false;
}
