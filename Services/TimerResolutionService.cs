using System.Runtime.InteropServices;

namespace SGuardLimiterMax.Services;

/// <summary>
/// A selectable timer resolution option shown in the UI dropdown.
/// Period100Ns is in 100-nanosecond units (e.g. 5000 = 0.5 ms, 10000 = 1 ms).
/// The sentinel value 0 means "system default — do not override."
/// </summary>
public record TimerResolutionOption(uint Period100Ns, string Label, string Description);

/// <summary>
/// Raises Windows timer resolution while a game session is active.
/// Uses NtSetTimerResolution (ntdll) which accepts 100 ns units and supports
/// sub-millisecond values (e.g. 0.5 ms), unlike the older timeBeginPeriod API.
/// </summary>
public static class TimerResolutionService
{
    [DllImport("ntdll.dll")]
    private static extern int NtSetTimerResolution(
        uint DesiredResolution, bool SetResolution, out uint CurrentResolution);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryTimerResolution(
        out uint MinimumResolution, out uint MaximumResolution, out uint CurrentResolution);

    private static bool _active;
    private static uint _period100Ns   = 10000; // 1 ms default
    private static uint _snapshot100Ns = 0;     // resolution captured just before first Enable()

    /// <summary>True while a raised timer period is held.</summary>
    public static bool IsActive => _active;

    /// <summary>
    /// Sentinel value in <see cref="Options"/> meaning "restore system default —
    /// call <see cref="Disable"/> rather than setting any period."
    /// </summary>
    public const uint PeriodSysDefault = 0;

    /// <summary>Available resolution options shown in the UI.</summary>
    public static IReadOnlyList<TimerResolutionOption> Options { get; } = BuildOptions();

    private static IReadOnlyList<TimerResolutionOption> BuildOptions()
    {
        string sysDefaultLabel = "系统默认";
        if (NtQueryTimerResolution(out _, out uint maxRes, out _) == 0 && maxRes > 0)
        {
            double ms = maxRes / 10000.0;
            sysDefaultLabel = $"系统默认（{ms:0.##} ms）";
        }

        return
        [
            new(5000,           "0.5 ms — 极限精度",  "Revision OS / 超频平台，或系统已原生支持 0.5ms 的场合"),
            new(10000,          "1 ms — 竞技主机首选", "追求最低调度延迟，适合高帧率独立显卡配置"),
            new(20000,          "2 ms — 均衡推荐",    "大多数游戏设备的最佳折衷点"),
            new(40000,          "4 ms — 轻薄本推荐",  "兼顾功耗与延迟，适合移动端 / 集显设备"),
            new(PeriodSysDefault, sysDefaultLabel,    "不干预 Windows 时钟，维持系统原始精度"),
        ];
    }

    /// <summary>
    /// Sets the system timer resolution to <paramref name="period100Ns"/> (100 ns units).
    /// On the first call, snapshots the current resolution so <see cref="Disable"/> can
    /// restore it exactly — preserving any pre-existing value set by the OS or another tool
    /// (e.g. Revision OS's 0.5 ms boot-time setting).
    /// </summary>
    public static void Enable(uint period100Ns)
    {
        if (_active && _period100Ns == period100Ns) return;

        // Snapshot the resolution that was active before we touched anything.
        if (!_active)
        {
            NtQueryTimerResolution(out _, out _, out uint current);
            _snapshot100Ns = current;
        }

        NtSetTimerResolution(period100Ns, true, out _);
        _period100Ns = period100Ns;
        _active = true;
    }

    /// <summary>
    /// Restores the timer resolution to the value captured before <see cref="Enable"/> was
    /// first called. On Revision OS this brings back the 0.5 ms boot value instead of
    /// falling through to the Windows default 15.6 ms.
    /// </summary>
    public static void Disable()
    {
        if (!_active) return;

        if (_snapshot100Ns > 0)
            NtSetTimerResolution(_snapshot100Ns, true, out _);
        else
            NtSetTimerResolution(0, false, out _);

        _active = false;
    }

    /// <summary>
    /// Queries both the current and system-default (maximum) timer resolution in one syscall.
    /// Returns (current, systemDefault) as formatted strings, e.g. "0.5 ms" / "15.63 ms".
    /// </summary>
    public static (string Current, string SystemDefault) QueryResolutions()
    {
        if (NtQueryTimerResolution(out _, out uint maxRes, out uint curRes) == 0)
        {
            string cur = $"{curRes / 10000.0:0.##} ms";
            string def = maxRes > 0 ? $"{maxRes / 10000.0:0.##} ms" : "未知";
            return (cur, def);
        }
        return ("未知", "未知");
    }

    /// <summary>Queries the current system-wide timer resolution as a display string.</summary>
    public static string QueryCurrentResolutionText()
    {
        var (cur, _) = QueryResolutions();
        return cur;
    }
}
