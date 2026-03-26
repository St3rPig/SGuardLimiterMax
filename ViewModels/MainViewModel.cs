using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using SGuardLimiterMax.Models;
using SGuardLimiterMax.Services;

namespace SGuardLimiterMax.ViewModels;

/// <summary>
/// Primary ViewModel for the Dashboard window.
/// Implements INotifyPropertyChanged for WPF data binding.
/// Owns the background monitoring loop and orchestrates service calls.
/// </summary>
public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    // ── State ─────────────────────────────────────────────────────────────────

    private AppConfig _config;
    private bool      _isGameRunning;
    private bool      _isDisposed;
    private string    _statusText = "Idle — waiting for game...";

    private readonly CancellationTokenSource _cts = new();
    private Task? _monitorTask;

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainViewModel()
    {
        _config = ConfigManager.Load();
        StartMonitor();
    }

    // ── Bindable properties ───────────────────────────────────────────────────

    public bool UnbindCPU
    {
        get => _config.UnbindCPU;
        set { _config.UnbindCPU = value; OnPropertyChanged(); ConfigManager.Save(_config); }
    }

    public bool OptimizePower
    {
        get => _config.OptimizePower;
        set { _config.OptimizePower = value; OnPropertyChanged(); ConfigManager.Save(_config); }
    }

    public bool FlushDNS
    {
        get => _config.FlushDNS;
        set { _config.FlushDNS = value; OnPropertyChanged(); ConfigManager.Save(_config); }
    }

    public bool KeepMonitoringAfterExit
    {
        get => _config.KeepMonitoringAfterExit;
        set { _config.KeepMonitoringAfterExit = value; OnPropertyChanged(); ConfigManager.Save(_config); }
    }

    public bool IsGameRunning
    {
        get => _isGameRunning;
        private set { _isGameRunning = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    // ── Manual trigger (e.g. from a "Apply Now" button) ──────────────────────

    public void ApplyOptimizationsNow()
    {
        ProcessOptimizer.ThrottleSGuard();
        ProcessOptimizer.BoostGameProcesses(_config.UnbindCPU);
    }

    // ── Background monitor ────────────────────────────────────────────────────

    private void StartMonitor()
    {
        _monitorTask = Task.Run(() => MonitorLoop(_cts.Token), _cts.Token);
    }

    private async Task MonitorLoop(CancellationToken ct)
    {
        bool wasRunning = false;

        while (!ct.IsCancellationRequested)
        {
            bool running = ProcessOptimizer.IsAnyGameRunning();

            if (running && !wasRunning)
                await Application.Current.Dispatcher.InvokeAsync(() => OnGameStarted());
            else if (!running && wasRunning)
                await Application.Current.Dispatcher.InvokeAsync(() => OnGameExited());

            wasRunning = running;

            try { await Task.Delay(3000, ct); }
            catch (TaskCanceledException) { break; }
        }
    }

    private void OnGameStarted()
    {
        IsGameRunning = true;
        StatusText    = "Game detected — optimizations active.";

        ProcessOptimizer.ThrottleSGuard();
        ProcessOptimizer.BoostGameProcesses(_config.UnbindCPU);

        if (_config.FlushDNS)
            PowerManager.FlushDns();

        if (_config.OptimizePower)
            PowerManager.ActivatePerformancePlan();
    }

    private void OnGameExited()
    {
        IsGameRunning = false;
        StatusText    = "Game exited — restoring defaults.";

        if (_config.OptimizePower)
            PowerManager.RestoreBalancedPlan();

        if (!_config.KeepMonitoringAfterExit)
        {
            StatusText = "Shutting down...";
            Application.Current.Shutdown();
        }
        else
        {
            StatusText = "Idle — waiting for game...";
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
