using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;
using Color = System.Windows.Media.Color;
using Grid = System.Windows.Controls.Grid;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Wrapper window that handles all ephemeral window lifecycle management:
///     Alt+Tab hiding, window deactivation detection, focus restoration, and closing logic.
/// </summary>
public class EphemeralWindow : Window {
    private readonly UserControl _contentControl;
    private readonly DispatcherTimer _ctrlKeyMonitor;
    private readonly bool _monitorCtrlKey;
    private readonly Action _onCtrlReleased;
    private bool _isClosing;

    public EphemeralWindow(
        UserControl content,
        string title = "Palette",
        bool monitorCtrlKey = false,
        Action onCtrlReleased = null
    ) {
        this._contentControl = content;
        this._monitorCtrlKey = monitorCtrlKey;
        this._onCtrlReleased = onCtrlReleased;
        this.Title = title;
        this.SizeToContent = SizeToContent.Manual;
        this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        this.WindowStyle = WindowStyle.None;
        this.AllowsTransparency = true;
        this.Background = Brushes.Transparent;
        this.ShowInTaskbar = false;
        this.Topmost = true;
        ThemeManager.LoadWpfUiResources(this);

        // Create main container grid with centered alignment
        var containerGrid = new Grid {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Add to grid (both at same location so pill floats over content)
        _ = containerGrid.Children.Add(
            new BorderSpec()
                .Border(UiSz.l, UiSz.ss)
                .Width(450, 450, 450)
                .Height(350, 350, 350)
                .DropShadow()
                .CreateAround(content));

        var titlePill = this.CreateTitlePill(title);
        titlePill.MouseLeftButtonDown += (_, _) => this.DragMove();
        _ = containerGrid.Children.Add(titlePill);

        this.Content = containerGrid;

        // 6. Apply custom implicit styles (focus visual, etc.)

        // Subscribe to CloseRequested event if content implements it
        if (content is ICloseRequestable closeable) closeable.CloseRequested += this.OnContentCloseRequested;

        // Set up Ctrl key monitoring if requested
        if (this._monitorCtrlKey) {
            this._ctrlKeyMonitor = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            this._ctrlKeyMonitor.Tick += this.OnCtrlKeyMonitorTick;
            this.Loaded += (_, _) => this._ctrlKeyMonitor.Start();
        }
    }

    private Border CreateTitlePill(string title) {
        var border = new BorderSpec()
            .Background(ThemeResource.ApplicationBackgroundBrush)
            .Border(UiSz.l, UiSz.ss)
            .HorizontalAlign(HorizontalAlignment.Left)
            .VerticalAlign(VerticalAlignment.Top)
            .Margin(0, -35, 0, 0) // More left and up positioning
            .Padding(UiSz.m, UiSz.l, UiSz.m, UiSz.l)
            .Width(200)
            .DropShadow()
            .CreateAround(new TextBlock {
                Text = title,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                Style = ThemeManager.GetTypographyStyle(FontTypography.Subtitle),
                Padding = new Thickness(0)
            });

        return border;
    }

    private void OnContentCloseRequested(object sender, CloseRequestedEventArgs e) =>
        this.CloseWindow(e.RestoreFocus);

    private void OnCtrlKeyMonitorTick(object sender, EventArgs e) {
        // Check if Ctrl key is still pressed (VK_CONTROL = 0x11)
        const int VK_CONTROL = 0x11;
        var isCtrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

        if (!isCtrlPressed) {
            // Ctrl key released - execute callback (if provided) then close the window
            this._ctrlKeyMonitor?.Stop();
            this._onCtrlReleased?.Invoke();
            this.CloseWindow();
        }
    }

    public void CloseWindow(bool restoreFocus = true) {
        // Debug.WriteLine($"[EphemeralWindow] CloseWindow called: restoreFocus={restoreFocus}, _isClosing={this._isClosing}");
        try {
            if (this._isClosing) return;
            this._isClosing = true;

            if (this._contentControl is ICloseRequestable closeable)
                closeable.CloseRequested -= this.OnContentCloseRequested;

            if (restoreFocus) RestoreRevitFocus();
            this.Close();
        } catch { }
    }

    /// <summary>
    ///     Restores focus to the main Revit window. Can be called from external code.
    /// </summary>
    public static void RestoreRevitFocus() {
        try {
            var revitProcess = Process.GetCurrentProcess();
            var revitHandle = revitProcess.MainWindowHandle;
            if (revitHandle != IntPtr.Zero) {
                var success = SetForegroundWindow(revitHandle);
            }
        } catch {
        }
    }

    protected override void OnClosing(CancelEventArgs e) {
        this._isClosing = true;
        this._ctrlKeyMonitor?.Stop();
        base.OnClosing(e);
    }

    #region Hiding from Alt+Tab and Window Messages

    protected override void OnSourceInitialized(EventArgs e) {
        base.OnSourceInitialized(e);

        // Remove window from Alt+Tab
        var helper = new WindowInteropHelper(this);
        _ = SetWindowLong(
            helper.Handle,
            GWL_EXSTYLE,
            GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_TOOLWINDOW
        );

        // Hook into window messages to detect activation changes
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(this.WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
        const int WM_ACTIVATE = 0x0006;
        const int WA_INACTIVE = 0;

        if (msg == WM_ACTIVATE) {
            var activateType = (int)wParam & 0xFFFF;
            // Debug.WriteLine($"[EphemeralWindow] WM_ACTIVATE: type={activateType} (0=inactive, 1=active, 2=click)");

            if (activateType == WA_INACTIVE && !this._isClosing) {
                // lParam contains the handle of the window being activated (may be zero)
                var newActiveWindow = lParam;
                var revitHandle = Process.GetCurrentProcess().MainWindowHandle;

                // Get actual foreground window (more reliable than lParam)
                var actualForegroundWindow = GetForegroundWindow();

                // Check if Alt key is pressed (Alt+Tab is active)
                var isAltTabActive = (GetAsyncKeyState(0x12) & 0x8000) != 0; // VK_MENU = 0x12

                // Determine the actual target window
                // If lParam is zero, use foreground window to determine what's actually being activated
                var targetWindow = newActiveWindow != IntPtr.Zero ? newActiveWindow : actualForegroundWindow;

                // Get our window handle to exclude it from consideration
                var helper = new WindowInteropHelper(this);
                var ourWindowHandle = helper.Handle;

                // If target is our own window or still zero, it's likely clicking outside or Alt+Tab
                // In that case, check if foreground is another app
                if (targetWindow == ourWindowHandle || targetWindow == IntPtr.Zero) {
                    if (actualForegroundWindow != IntPtr.Zero &&
                        actualForegroundWindow != ourWindowHandle &&
                        actualForegroundWindow != revitHandle) {
                        // Foreground is another app - user is switching away
                        targetWindow = actualForegroundWindow;
                    }
                }

                // Check if user is switching to a different window (not Revit, not our window)
                var isSwitchingToOtherApp = targetWindow != IntPtr.Zero &&
                                            targetWindow != revitHandle &&
                                            targetWindow != ourWindowHandle;

                // If clicking Revit but it's already the foreground window, don't restore focus
                var isRevitAlreadyForeground = targetWindow == revitHandle && actualForegroundWindow == revitHandle;

                // Don't restore focus if: Alt+Tab is active, switching to another app, or Revit is already foreground
                var shouldRestoreFocus = !isAltTabActive && !isSwitchingToOtherApp && !isRevitAlreadyForeground;

                var actionType = isAltTabActive
                    ? $"Alt+Tab active (target: {this.GetWindowTitle(targetWindow)})"
                    : targetWindow == IntPtr.Zero
                        ? "Click outside (desktop/void)"
                        : targetWindow == revitHandle
                            ? isRevitAlreadyForeground
                                ? "Clicking Revit (already foreground)"
                                : "Switching to Revit"
                            : $"Switching to: {this.GetWindowTitle(targetWindow)}";

                // Debug.WriteLine($"[EphemeralWindow] Action: {actionType} → Close (restoreFocus={shouldRestoreFocus})");

                // Use Dispatcher to avoid issues with closing during message processing
                _ = this.Dispatcher.BeginInvoke(new Action(() => {
                    if (!this._isClosing) this.CloseWindow(shouldRestoreFocus);
                }));
            }
        }

        return IntPtr.Zero;
    }

    private string GetWindowTitle(IntPtr hwnd) {
        if (hwnd == IntPtr.Zero) return "null";

        try {
            const int maxLength = 256;
            var title = new StringBuilder(maxLength);
            _ = GetWindowText(hwnd, title, maxLength);
            var titleText = title.ToString();

            if (string.IsNullOrEmpty(titleText)) {
                // Try to get process name instead
                _ = GetWindowThreadProcessId(hwnd, out var processId);
                try {
                    var process = Process.GetProcessById((int)processId);
                    return $"[Process: {process.ProcessName}]";
                } catch {
                    return $"[HWND: {hwnd}]";
                }
            }

            return titleText;
        } catch {
            return $"[HWND: {hwnd}]";
        }
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    #endregion
}

/// <summary>
///     Event args for close requests
/// </summary>
public class CloseRequestedEventArgs : EventArgs {
    public bool RestoreFocus { get; init; } = true;
}

/// <summary>
///     Interface for UserControls that can request their parent window to close.
/// </summary>
public interface ICloseRequestable {
    event EventHandler<CloseRequestedEventArgs> CloseRequested;
}