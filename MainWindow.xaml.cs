using System.Windows;
using System.Windows.Forms; // Required for NotifyIcon (System.Windows.Forms nuget or framework ref)
using SGuardLimiterMax.ViewModels;

namespace SGuardLimiterMax;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    // System tray icon — Gemini should assign an icon image.
    // Requires reference: System.Windows.Forms (add via csproj if needed)
    // private NotifyIcon? _trayIcon;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // ── System Tray setup (skeleton — Gemini fills icon/menu) ────────────
        // _trayIcon = new NotifyIcon
        // {
        //     Icon    = new System.Drawing.Icon("Assets/icon.ico"),
        //     Visible = true,
        //     Text    = "SGuard Limiter Max",
        // };
        // _trayIcon.DoubleClick += (_, _) => { Show(); WindowState = WindowState.Normal; };

        // Minimize to tray instead of closing
        // Closing += OnWindowClosing;
    }

    // private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    // {
    //     e.Cancel = true;
    //     Hide();
    // }

    protected override void OnClosed(EventArgs e)
    {
        // _trayIcon?.Dispose();
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
