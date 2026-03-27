using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;
using SGuardLimiterMax.Models;
using SGuardLimiterMax.Services;

namespace SGuardLimiterMax.ViewModels;

/// <summary>
/// Primary ViewModel for the Dashboard window.
/// </summary>
public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private AppConfig        _config;
    private bool             _isGameRunning;
    private bool             _isDisposed;
    private string           _statusText = "[就绪] 等待目标游戏进程启动...";
    private string           _activePowerPlanName = "查询中...";
    private List<GameInfo>   _lastActiveGames = [];

    private readonly CancellationTokenSource _cts = new();
    private Task? _monitorTask;
    private readonly DispatcherTimer _timerResolutionPoller;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised on the UI thread when the app should minimize to tray.</summary>
    public event Action? EnterMonitorRequested;

    /// <summary>Raised when a game session is detected. Carries display names and optimization summary.</summary>
    public event Action<List<GameInfo>, string>? GameDetected;

    /// <summary>
    /// Raised when all monitored games exit and <c>ExitWithGame</c> is false (app stays resident).
    /// Carries the names of the games that exited.
    /// </summary>
    public event Action<string>? GameExited;

    /// <summary>Raised on the UI thread when the app should shut down (e.g. ExitWithGame triggered).</summary>
    public event Action? ExitRequested;

    // ── Construction ──────────────────────────────────────────────────────────

    public MainViewModel()
    {
        _config = ConfigManager.Load();

        // Sync startup entry with current exe path on every launch.
        StartupManager.Sync(_config.AutoStart);

        // Push custom games into the optimizer.
        ProcessOptimizer.UpdateCustomGames(_config.CustomGames);

        // Populate the observable collections.
        foreach (var g in _config.CustomGames)
            CustomGames.Add(g);

        // Load power plans in background; don't block the UI thread.
        _ = RefreshPowerPlansAsync();

        // Poll the system timer resolution every 2 s so the UI stays live.
        _timerResolutionPoller = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timerResolutionPoller.Tick += (_, _) => OnPropertyChanged(nameof(ActiveTimerResolutionText));
        _timerResolutionPoller.Start();

        StartMonitor();
    }

    // ── Feature toggles ───────────────────────────────────────────────────────

    public bool ThrottleSGuard
    {
        get => _config.ThrottleSGuard;
        set { _config.ThrottleSGuard = value; OnPropertyChanged(); }
    }

    public bool BoostGamePriority
    {
        get => _config.BoostGamePriority;
        set { _config.BoostGamePriority = value; OnPropertyChanged(); }
    }

    public bool UnbindCPU
    {
        get => _config.UnbindCPU;
        set { _config.UnbindCPU = value; OnPropertyChanged(); }
    }

    public bool OptimizePower
    {
        get => _config.OptimizePower;
        set { _config.OptimizePower = value; OnPropertyChanged(); }
    }

    public bool FlushDNS
    {
        get => _config.FlushDNS;
        set { _config.FlushDNS = value; OnPropertyChanged(); }
    }

    public bool TimerResolution
    {
        get => _config.TimerResolution;
        set
        {
            _config.TimerResolution = value;
            OnPropertyChanged();
            if (!value) TimerResolutionService.Disable();
        }
    }

    public bool AutoMinimizeOnGame
    {
        get => _config.AutoMinimizeOnGame;
        set { _config.AutoMinimizeOnGame = value; OnPropertyChanged(); }
    }

    public bool ExitWithGame
    {
        get => _config.ExitWithGame;
        set { _config.ExitWithGame = value; OnPropertyChanged(); }
    }

    public bool ShowNotifications
    {
        get => _config.ShowNotifications;
        set { _config.ShowNotifications = value; OnPropertyChanged(); }
    }

    public bool RestorePowerOnExit
    {
        get => _config.RestorePowerOnExit;
        set { _config.RestorePowerOnExit = value; OnPropertyChanged(); }
    }

    public bool RestoreTimerOnExit
    {
        get => _config.RestoreTimerOnExit;
        set { _config.RestoreTimerOnExit = value; OnPropertyChanged(); }
    }

    public bool AutoStart
    {
        get => _config.AutoStart;
        set
        {
            _config.AutoStart = value;
            OnPropertyChanged();
            StartupManager.Sync(value);
        }
    }

    // ── Power plan ────────────────────────────────────────────────────────────

    /// <summary>
    /// Currently active Windows power plan name (queried from the system).
    /// </summary>
    public string ActivePowerPlanName
    {
        get => _activePowerPlanName;
        private set { _activePowerPlanName = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// All power plans installed on this system. Bound to a dropdown in the UI.
    /// </summary>
    public ObservableCollection<PowerPlanInfo> AvailablePowerPlans { get; } = [];

    /// <summary>
    /// The GUID of the plan to activate when a game session starts.
    /// Null means auto-select (Ultimate → High Performance).
    /// </summary>
    public string? TargetPowerPlanGuid
    {
        get => _config.TargetPowerPlanGuid;
        set
        {
            _config.TargetPowerPlanGuid = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Re-queries all power plans from the OS and refreshes <see cref="AvailablePowerPlans"/>
    /// and <see cref="ActivePowerPlanName"/>. Safe to call from any thread.
    /// </summary>
    public async Task RefreshPowerPlansAsync()
    {
        var plans = await Task.Run(PowerManager.GetAllPlans);

        // Application.Current can be null if this task completes during shutdown.
        var app = Application.Current;
        if (app == null) return;

        try
        {
            await app.Dispatcher.InvokeAsync(() =>
            {
                AvailablePowerPlans.Clear();
                foreach (var p in plans)
                    AvailablePowerPlans.Add(p);

                var active = plans.FirstOrDefault(p => p.IsActive);
                ActivePowerPlanName = active?.Name ?? "未知";
                OnPropertyChanged(nameof(SelectedTargetPlan));
            });
        }
        catch (TaskCanceledException) { /* Dispatcher shut down — ignore */ }
    }

    /// <summary>
    /// Two-way ComboBox binding: resolves TargetPowerPlanGuid → PowerPlanInfo object.
    /// Setting to null clears the target (auto-select best plan on next game start).
    /// </summary>
    public PowerPlanInfo? SelectedTargetPlan
    {
        get => AvailablePowerPlans.FirstOrDefault(p => p.Guid == _config.TargetPowerPlanGuid);
        set
        {
            _config.TargetPowerPlanGuid = value?.Guid;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TargetPowerPlanGuid));
        }
    }

    // ── Timer resolution ──────────────────────────────────────────────────────

    /// <summary>Whether the 1ms timer resolution is currently held.</summary>
    public bool IsTimerResolutionActive => TimerResolutionService.IsActive;

    /// <summary>Human-readable current system timer resolution (e.g. "15.6 ms" or "1 ms").</summary>
    public string ActiveTimerResolutionText => TimerResolutionService.QueryCurrentResolutionText();

    /// <summary>Available timer resolution options bound to the UI dropdown.</summary>
    public IReadOnlyList<TimerResolutionOption> TimerResolutionOptions => TimerResolutionService.Options;

    /// <summary>
    /// Two-way ComboBox binding: resolves TimerResolutionPeriod100Ns → TimerResolutionOption.
    /// </summary>
    public TimerResolutionOption? SelectedTimerResolutionOption
    {
        get => TimerResolutionService.Options.FirstOrDefault(o => o.Period100Ns == _config.TimerResolutionPeriod100Ns);
        set
        {
            if (value == null) return;
            _config.TimerResolutionPeriod100Ns = value.Period100Ns;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Immediately applies the selected timer resolution (if the feature is enabled) and persists config.
    /// Period100Ns == 0 (PeriodSysDefault) restores the system default via Disable().
    /// </summary>
    public void ApplyTimerResolution()
    {
        EnableTimerResolutionInternal();

        OnPropertyChanged(nameof(IsTimerResolutionActive));
        OnPropertyChanged(nameof(ActiveTimerResolutionText));
        ConfigManager.Save(_config);
    }

    /// <summary>
    /// Shared helper: enables (or restores) timer resolution according to the configured period.
    /// Period100Ns == 0 → Disable (restore system default).
    /// </summary>
    private void EnableTimerResolutionInternal()
    {
        if (_config.TimerResolutionPeriod100Ns == TimerResolutionService.PeriodSysDefault)
            TimerResolutionService.Disable();
        else
            TimerResolutionService.Enable(_config.TimerResolutionPeriod100Ns);
    }

    // ── Custom games ──────────────────────────────────────────────────────────

    /// <summary>User-defined game entries bound to the custom game list UI.</summary>
    public ObservableCollection<CustomGameEntry> CustomGames { get; } = [];

    /// <summary>
    /// Adds a new custom game entry, persists it, and updates the optimizer.
    /// Silently ignores entries with empty process names or duplicates.
    /// </summary>
    public void AddCustomGame(string processName, string displayName,
                              bool boostPriority = true, bool unbindCpu0 = false)
    {
        processName = processName.Trim();
        if (string.IsNullOrEmpty(processName)) return;

        // Strip .exe suffix if present.
        if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            processName = processName[..^4];

        bool duplicate = _config.CustomGames.Any(
            g => string.Equals(g.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
        if (duplicate) return;

        var entry = new CustomGameEntry
        {
            ProcessName   = processName,
            DisplayName   = string.IsNullOrWhiteSpace(displayName) ? processName : displayName.Trim(),
            BoostPriority = boostPriority,
            UnbindCpu0    = unbindCpu0,
        };

        _config.CustomGames.Add(entry);
        CustomGames.Add(entry);
        ProcessOptimizer.UpdateCustomGames(_config.CustomGames);
        ConfigManager.Save(_config);
    }

    /// <summary>
    /// Removes a custom game entry by process name, persists the change, and
    /// updates the optimizer.
    /// </summary>
    public void RemoveCustomGame(CustomGameEntry entry)
    {
        _config.CustomGames.Remove(entry);
        CustomGames.Remove(entry);
        ProcessOptimizer.UpdateCustomGames(_config.CustomGames);
        ConfigManager.Save(_config);
    }

    // ── Status ────────────────────────────────────────────────────────────────

    public bool IsGameRunning
    {
        get => _isGameRunning;
        private set { _isGameRunning = value; OnPropertyChanged(); }
    }

    /// <summary>True if the power plan was switched this session and not yet restored.</summary>
    public bool IsPowerActivated => _config.OptimizePower && PowerManager.IsActivated;

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    // ── Commands / actions ────────────────────────────────────────────────────

    /// <summary>Persists the current toggle state to disk.</summary>
    public void SaveConfig() => ConfigManager.Save(_config);

    /// <summary>
    /// Immediately switches to the specified power plan.
    /// Pass <c>null</c> to auto-select the best available performance plan.
    /// This can be called any time — not just during a game session.
    /// The UI just picks a plan from <see cref="AvailablePowerPlans"/> and passes its Guid here.
    /// </summary>
    public void ApplyPowerPlan(string? guid)
    {
        PowerManager.ActivatePerformancePlan(guid);
        OnPropertyChanged(nameof(IsPowerActivated));
        _ = RefreshPowerPlansAsync();
    }

    /// <summary>
    /// Saves config, immediately applies all enabled optimizations, and returns
    /// a human-readable summary of what was executed and what was skipped.
    /// </summary>
    public string ApplyNow()
    {
        ConfigManager.Save(_config);

        var running  = ProcessOptimizer.GetRunningGames();
        var applied  = new List<string>();
        var disabled = new List<string>();

        if (_config.ThrottleSGuard)
        {
            ProcessOptimizer.ThrottleSGuard();
            applied.Add("SGuard 进程限制（Idle 优先级 + 末位核心）");
        }
        else disabled.Add("SGuard 进程限制");

        if (_config.BoostGamePriority || _config.UnbindCPU)
            ProcessOptimizer.ApplyGameSettings(_config.BoostGamePriority, _config.UnbindCPU);

        if (_config.BoostGamePriority)  applied.Add("游戏进程优先级提升（High 级）");
        else                            disabled.Add("游戏进程优先级提升");

        if (_config.UnbindCPU)  applied.Add("游戏进程核心 0 解绑");
        else                    disabled.Add("游戏进程核心 0 解绑");

        if (_config.FlushDNS)
        {
            PowerManager.FlushDns();
            applied.Add("DNS 缓存刷新");
        }
        else disabled.Add("DNS 缓存刷新");

        if (_config.OptimizePower)
        {
            PowerManager.ActivatePerformancePlan(_config.TargetPowerPlanGuid);
            applied.Add("高性能电源计划激活");
        }
        else disabled.Add("高性能电源计划");

        if (_config.TimerResolution && running.Count > 0)
        {
            EnableTimerResolutionInternal();
            string periodLabel = _config.TimerResolutionPeriod100Ns == TimerResolutionService.PeriodSysDefault
                ? "系统默认精度（不干预）"
                : $"{_config.TimerResolutionPeriod100Ns / 10000.0:0.##} ms 计时器精度";
            applied.Add(periodLabel);
        }
        else if (_config.TimerResolution && running.Count == 0)
            disabled.Add("计时器精度（目标游戏未运行）");
        else
            disabled.Add("系统计时器精度提升");

        var lines = new List<string>();

        if (running.Count > 0)
        {
            string names = string.Join("、", running.Select(g => g.DisplayName));
            lines.Add($"检测到游戏：{names}");
        }
        else
        {
            lines.Add("未检测到目标游戏进程（优化仍已写入）");
        }
        lines.Add(string.Empty);

        if (applied.Count > 0)
        {
            lines.Add("已执行：");
            lines.AddRange(applied.Select(x => "  ✓  " + x));
        }

        if (disabled.Count > 0)
        {
            if (applied.Count > 0) lines.Add(string.Empty);
            lines.Add("已跳过（开关未启用）：");
            lines.AddRange(disabled.Select(x => "  ○  " + x));
        }

        return string.Join("\n", lines);
    }

    // ── Monitor loop ──────────────────────────────────────────────────────────

    private void StartMonitor()
    {
        _monitorTask = Task.Run(() => MonitorLoop(_cts.Token), _cts.Token);
    }

    private async Task MonitorLoop(CancellationToken ct)
    {
        try
        {
            var initialGames = ProcessOptimizer.GetRunningGames();
            var previous     = new HashSet<string>(initialGames.Select(g => g.ProcessName));

            if (initialGames.Count > 0)
                await DispatchAsync(() => UpdateActiveStatus(initialGames));

            while (!ct.IsCancellationRequested)
            {
                var current    = ProcessOptimizer.GetRunningGames();
                var currentSet = new HashSet<string>(current.Select(g => g.ProcessName));

                if (currentSet.Count > 0 && _config.ThrottleSGuard)
                    ProcessOptimizer.ThrottleSGuard();

                var started    = current.Where(g => !previous.Contains(g.ProcessName)).ToList();
                bool allExited  = previous.Count > 0 && currentSet.Count == 0;
                bool someExited = previous.Count > 0 && previous.Count != currentSet.Count && currentSet.Count > 0;

                if (started.Count > 0)
                    await DispatchAsync(() => OnGamesStarted(current));
                else if (allExited)
                    await DispatchAsync(() => OnAllGamesExited(previous));
                else if (someExited)
                    await DispatchAsync(() => UpdateActiveStatus(current));

                previous = currentSet;

                try { await Task.Delay(3000, ct); }
                catch (TaskCanceledException) { break; }
            }
        }
        catch (Exception) when (_isDisposed || ct.IsCancellationRequested)
        {
            // App is shutting down — swallow any dispatcher-related exceptions.
        }
    }

    /// <summary>
    /// Marshals an action to the UI dispatcher. Returns immediately if the
    /// app is shutting down (Application.Current is null or dispatcher is disabled).
    /// </summary>
    private static Task DispatchAsync(Action action)
    {
        var app = Application.Current;
        if (app == null) return Task.CompletedTask;
        return app.Dispatcher.InvokeAsync(action).Task;
    }

    private void OnGamesStarted(List<GameInfo> games)
    {
        IsGameRunning = true;

        if (_config.ThrottleSGuard)
            ProcessOptimizer.ThrottleSGuard();

        if (_config.BoostGamePriority || _config.UnbindCPU)
            ProcessOptimizer.ApplyGameSettings(_config.BoostGamePriority, _config.UnbindCPU);

        if (_config.FlushDNS)
            PowerManager.FlushDns();

        if (_config.OptimizePower)
            PowerManager.ActivatePerformancePlan(_config.TargetPowerPlanGuid);

        if (_config.TimerResolution)
            EnableTimerResolutionInternal();

        UpdateActiveStatus(games);
        GameDetected?.Invoke(games, BuildOptimizationSummary());

        if (_config.AutoMinimizeOnGame)
            EnterMonitorRequested?.Invoke();
    }

    private void OnAllGamesExited(HashSet<string> exitedProcessNames)
    {
        IsGameRunning = false;

        // Build a display-name string from the last known active game list.
        string exitedNames = _lastActiveGames.Count > 0
            ? string.Join("、", _lastActiveGames.Select(g => g.DisplayName))
            : "游戏";
        _lastActiveGames = [];

        if (_config.TimerResolution)
        {
            if (_config.RestoreTimerOnExit)
                TimerResolutionService.Disable();
        }

        if (_config.OptimizePower)
        {
            if (_config.RestorePowerOnExit)
                PowerManager.RestoreOriginalPlan();
            else
                PowerManager.DiscardRestore();
        }

        OnPropertyChanged(nameof(IsPowerActivated));
        OnPropertyChanged(nameof(IsTimerResolutionActive));

        bool restoredAny = (_config.TimerResolution && _config.RestoreTimerOnExit) ||
                           (_config.OptimizePower && _config.RestorePowerOnExit);
        string suffix = restoredAny ? "，优化已还原。" : "。";
        GameExited?.Invoke($"{exitedNames} 已退出{suffix}");

        if (_config.ExitWithGame)
        {
            StatusText = "[退出中] 游戏已关闭，程序即将退出...";

            // Delay shutdown so the balloon tip has time to be seen (~2.5 s).
            // Route through ExitRequested so MainWindow can run its full cleanup.
            _ = Task.Run(async () =>
            {
                await Task.Delay(2500);
                await DispatchAsync(() => ExitRequested?.Invoke());
            });
        }
        else
        {
            StatusText = "[就绪] 游戏已退出，等待目标游戏进程启动...";
            _ = RefreshPowerPlansAsync();
        }
    }

    private string BuildOptimizationSummary()
    {
        var items = new List<string>();
        if (_config.ThrottleSGuard)     items.Add("SGuard 已限制");
        if (_config.BoostGamePriority)  items.Add("进程优先级 High");
        if (_config.UnbindCPU)          items.Add("核心 0 已解绑");
        if (_config.OptimizePower)      items.Add("高性能电源");
        if (_config.FlushDNS)           items.Add("DNS 已刷新");
        if (_config.TimerResolution)    items.Add("计时器 1ms");
        return items.Count > 0 ? string.Join(" · ", items) : "（所有优化已关闭）";
    }

    private void UpdateActiveStatus(List<GameInfo> games)
    {
        IsGameRunning    = games.Count > 0;
        _lastActiveGames = games;
        string names = string.Join(" · ", games.Select(g => g.DisplayName));
        StatusText = _config.ThrottleSGuard
            ? $"[生效中] {names} · SGuard 已限制"
            : $"[生效中] {names} · 优化已应用";
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _timerResolutionPoller.Stop();
        _cts.Cancel();
        _cts.Dispose();
        if (_config.RestoreTimerOnExit)
            TimerResolutionService.Disable();
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
