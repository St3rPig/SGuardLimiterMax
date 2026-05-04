using System.Windows;
using System.Windows.Input;

namespace SGuardLimiterMax.Views;

public partial class ResultDialog : Window
{
    public ResultDialog(string message)
    {
        InitializeComponent();
        TxtSummary.Text = message;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        => Close();

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Escape)
            Close();
        base.OnKeyDown(e);
    }
}
