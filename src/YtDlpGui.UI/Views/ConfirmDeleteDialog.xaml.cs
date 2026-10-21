using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using YtDlpGui.UI.Theming;

namespace YtDlpGui.UI.Views;

public partial class ConfirmDeleteDialog : Window
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    public ConfirmDeleteDialog(string message = "Удалить файл с компьютера?")
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (ThemeManager.IsDark)
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                var value = 1;
                _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
            }
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static bool Show(string message = "Удалить файл с компьютера?", Window? owner = null)
    {
        try
        {
            var dialog = new ConfirmDeleteDialog(message);
            dialog.Owner = owner ?? System.Windows.Application.Current?.MainWindow;
            return dialog.ShowDialog() == true;
        }
        catch
        {
            return MessageBox.Show(message, "Scachalka", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
