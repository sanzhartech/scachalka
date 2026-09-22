using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using YtDlpGui.UI.Theming;
using YtDlpGui.UI.ViewModels;

namespace YtDlpGui.UI.Views;

/// <summary>
/// Code-behind is intentionally thin: drag &amp; drop plumbing (a pure view concern)
/// and the Win11 dark title bar. All logic lives in <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        TryApplyWindowIcon();

        ThemeManager.ThemeChanged += OnThemeChanged;
        Closed += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
    }

    private void TryApplyWindowIcon()
    {
        try
        {
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                new Uri("pack://application:,,,/YtDlpGui.UI;component/Assets/scachalka.ico", UriKind.Absolute));
        }
        catch (Exception)
        {
            // Icon resource absent (icon not generated yet) — run tools/generate-icon.ps1.
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyTitleBarTheme(ThemeManager.IsDark);
    }

    private void OnThemeChanged(bool isDark) => ApplyTitleBarTheme(isDark);

    private void ApplyTitleBarTheme(bool isDark)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return; // Window not created yet; OnSourceInitialized will call again.
        }

        var value = isDark ? 1 : 0;
        // Best effort: older Windows 10 builds ignore this attribute.
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = HasText(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        var text = ExtractText(e.Data);
        if (!string.IsNullOrWhiteSpace(text))
        {
            _viewModel.AddDroppedText(text);
        }

        e.Handled = true;
    }

    private static bool HasText(IDataObject data) =>
        data.GetDataPresent(DataFormats.UnicodeText)
        || data.GetDataPresent(DataFormats.Text)
        || data.GetDataPresent(DataFormats.StringFormat);

    private static string? ExtractText(IDataObject data)
    {
        try
        {
            if (data.GetDataPresent(DataFormats.UnicodeText))
            {
                return data.GetData(DataFormats.UnicodeText) as string;
            }

            if (data.GetDataPresent(DataFormats.Text))
            {
                return data.GetData(DataFormats.Text) as string;
            }

            return null;
        }
        catch (COMException)
        {
            // The drag source failed to render its data — treat as an empty drop.
            return null;
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) =>
        Close();

    private void OnSettingsClick(object sender, RoutedEventArgs e) =>
        _viewModel.IsAdvOptionsExpanded = !_viewModel.IsAdvOptionsExpanded;

    private void OnJobDotsClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.ContextMenu is not null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
