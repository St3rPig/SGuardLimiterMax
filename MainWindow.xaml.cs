using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using SGuardLimiterMax.Models;
using SGuardLimiterMax.Services;

namespace SGuardLimiterMax
{
    public partial class MainWindow : Window
    {
        private NotifyIcon?                _trayIcon;
        private ViewModels.MainViewModel? _vm;

        public MainWindow()
        {
            InitializeComponent();

            _vm = new ViewModels.MainViewModel();
            this.DataContext = _vm;
            _vm.EnterMonitorRequested += EnterMonitorMode;
            _vm.GameDetected          += OnGameDetected;
            _vm.GameExited            += OnGameExited;
            _vm.ExitRequested         += ShutdownApp;

            InitializeTrayIcon();

            if (App.IsAutoStart)
                EnterMonitorMode();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Intercept Alt+F4 and any other Window.Close() calls so cleanup always runs.
            e.Cancel = true;
            ShutdownApp();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private static System.Drawing.Icon LoadTrayIcon()
        {
            try
            {
                var info = Application.GetResourceStream(
                    new Uri("pack://application:,,,/Assets/SGuard_Limiter_Max.png"));
                if (info != null)
                {
                    using var bmp = new System.Drawing.Bitmap(info.Stream);
                    using var sized = new System.Drawing.Bitmap(bmp, 32, 32);
                    return System.Drawing.Icon.FromHandle(sized.GetHicon());
                }
            }
            catch { }
            return System.Drawing.SystemIcons.Shield;
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Icon    = LoadTrayIcon(),
                Text    = "SGuard Limiter Max",
                Visible = false
            };

            _trayIcon.DoubleClick += (s, e) => ShowWindow();

            var menu = new ContextMenuStrip();
            menu.Items.Add("打开面板", null, (s, e) => ShowWindow());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => ShutdownApp());
            _trayIcon.ContextMenuStrip = menu;
        }

        private void OnGameDetected(System.Collections.Generic.List<SGuardLimiterMax.Services.GameInfo> games, string summary)
        {
            if (_trayIcon == null) return;

            string names = string.Join(" · ", System.Linq.Enumerable.Select(games, g => g.DisplayName));
            _trayIcon.Text = $"SGuard Limiter Max · {names}";

            if (_vm?.ShowNotifications == true)
            {
                // Show balloon if already in tray, or if AutoMinimizeOnGame will hide the window
                // right after this event fires (window is still visible at this point).
                bool willHide = _vm.AutoMinimizeOnGame;
                if (!this.IsVisible || willHide)
                {
                    _trayIcon.ShowBalloonTip(
                        4000,
                        $"检测到游戏 · {names}",
                        summary,
                        ToolTipIcon.None);
                }
            }
        }

        private void OnGameExited(string message)
        {
            if (_trayIcon == null) return;
            _trayIcon.Text = "SGuard Limiter Max";
            if (_vm?.ShowNotifications == true)
                _trayIcon.ShowBalloonTip(3000, "SGuard Limiter Max", message, ToolTipIcon.None);
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => this.WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => ShutdownApp();

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
            => _vm?.SaveConfig();

        private void BtnApplyNow_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            string summary = _vm.ApplyNow();
            MessageBox.Show(summary, "执行完成", MessageBoxButton.OK, MessageBoxImage.None);
        }

        private void BtnEnterMonitor_Click(object sender, RoutedEventArgs e)
        {
            _vm?.SaveConfig();
            EnterMonitorMode();
        }

        private void BtnApplyPlan_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.ApplyPowerPlan(_vm.TargetPowerPlanGuid);
            _vm.SaveConfig();
        }

        private void BtnRefreshPlans_Click(object sender, RoutedEventArgs e)
            => _ = _vm?.RefreshPowerPlansAsync();

        private void BtnApplyTimer_Click(object sender, RoutedEventArgs e)
            => _vm?.ApplyTimerResolution();

        private void BtnAddGame_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;

            string processName = TxtProcessName.Text.Trim();
            if (string.IsNullOrEmpty(processName))
            {
                MessageBox.Show("请填写进程名。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _vm.AddCustomGame(
                processName,
                TxtDisplayName.Text.Trim(),
                ChkBoostPriority.IsChecked == true,
                ChkUnbindCpu0.IsChecked == true);

            TxtProcessName.Text = string.Empty;
            TxtDisplayName.Text = string.Empty;
        }

        private void BtnRemoveGame_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            if (((System.Windows.Controls.Button)sender).Tag is CustomGameEntry entry)
                _vm.RemoveCustomGame(entry);
        }

        private void EnterMonitorMode()
        {
            bool wasVisible = this.IsVisible;
            this.Hide();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = true;
                if (wasVisible && _vm?.ShowNotifications == true)
                    _trayIcon.ShowBalloonTip(2000, "SGuard Limiter Max",
                        "已最小化至托盘，程序将在后台持续监控。", ToolTipIcon.None);
            }
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Text    = "SGuard Limiter Max";
            }
        }

        private void ShutdownApp()
        {
            if (_vm?.RestorePowerOnExit == true)
                PowerManager.RestoreOriginalPlan();
            else
                PowerManager.DiscardRestore();

            _vm?.Dispose();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            Application.Current.Shutdown();
        }
    }
}
