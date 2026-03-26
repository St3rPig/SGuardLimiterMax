namespace SGuardLimiterMax.Models;

/// <summary>
/// Serializable configuration model. Stored as Config.json next to the executable.
/// </summary>
public class AppConfig
{
    /// <summary>Unbind game process from CPU 0 (Affinity &amp;= ~1).</summary>
    public bool UnbindCPU { get; set; } = false;

    /// <summary>Switch to Ultimate/High Performance power plan while game is running.</summary>
    public bool OptimizePower { get; set; } = true;

    /// <summary>Run ipconfig /flushdns when game session starts.</summary>
    public bool FlushDNS { get; set; } = true;

    /// <summary>Keep the monitor loop alive after game exits instead of shutting the app down.</summary>
    public bool KeepMonitoringAfterExit { get; set; } = false;
}
