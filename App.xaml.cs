using System.Windows;

namespace SGuardLimiterMax;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Global exception handler to prevent silent crashes.
        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(ex.Exception.Message, "SGuard Limiter Max — Unexpected Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };
    }
}
