using System.Threading;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace SGuardLimiterMax;

public partial class App : Application
{
    private static Mutex? _mutex;

    public static bool IsAutoStart { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "SGuardLimiterMax_SingleInstance", out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("SGuard Limiter Max 已在运行中。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        IsAutoStart = Array.Exists(e.Args, a =>
            string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));

        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(ex.Exception.Message, "SGuard Limiter Max — 意外错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
