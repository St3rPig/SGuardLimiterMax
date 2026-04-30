using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using SGuardLimiterMax.Services;

namespace SGuardLimiterMax;

public partial class App : Application
{
    private static Mutex? _mutex;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    public static bool IsAutoStart { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        ThemeManager.Initialize();

        _mutex = new Mutex(true, "SGuardLimiterMax_SingleInstance", out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("SGuard Limiter 已在运行中。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        IsAutoStart = Array.Exists(e.Args, a =>
            string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));

        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(ex.Exception.Message, "SGuard Limiter — 意外错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        // Manually create the main window so we can skip Show() on autostart,
        // avoiding any visible flash before the window hides itself.
        var window = new MainWindow();
        MainWindow = window;
        if (!IsAutoStart)
            window.Show();

        // Notify Windows shell to refresh icon cache for this app
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
