using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace AddinCmdPalette.Core;

/// <summary>
///     Wrapper window that handles all ephemeral window lifecycle management:
///     Alt+Tab hiding, window deactivation detection, focus restoration, and closing logic.
/// </summary>
public class EphemeralWindow : Window {
    private readonly UserControl _contentControl;
    private bool _isClosing;

    public EphemeralWindow(UserControl content, string title = "Palette") {
        this._contentControl = content;
        this.Title = title;
        this.Width = 600;
        this.Height = 400;
        this.MinWidth = 500;
        this.MinHeight = 300;
        this.SizeToContent = SizeToContent.Manual;
        this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        this.WindowStyle = WindowStyle.None;
        this.AllowsTransparency = true;
        this.Background = Brushes.Transparent;
        this.ShowInTaskbar = false;
        this.Topmost = true;

        this.Content = content;

        // Subscribe to CloseRequested event if content implements it
        if (content is ICloseRequestable closeable) closeable.CloseRequested += this.OnContentCloseRequested;
    }

    private void OnContentCloseRequested(object sender, EventArgs e) => this.CloseWindow();

    public void CloseWindow(bool restoreFocus = true) {
        // Debug.WriteLine($"[EphemeralWindow] CloseWindow called: restoreFocus={restoreFocus}, _isClosing={this._isClosing}");
        try {
            if (this._isClosing) {
                // Debug.WriteLine("[EphemeralWindow] CloseWindow: Already closing, aborting");
                return;
            }

            this._isClosing = true;

            // Unsubscribe from close events
            if (this._contentControl is ICloseRequestable closeable)
                closeable.CloseRequested -= this.OnContentCloseRequested;

            // Restore focus to Revit before closing (unless user is switching to another app)
            if (restoreFocus) {
                // Debug.WriteLine("[EphemeralWindow] CloseWindow: Restoring focus to Revit");
                this.RestoreRevitFocus();
            }

            // Debug.WriteLine("[EphemeralWindow] CloseWindow: Skipping focus restore (user switching apps)");
            // Debug.WriteLine("[EphemeralWindow] CloseWindow: Calling Window.Close()");
            this.Close();
        } catch {
            // Debug.WriteLine($"[EphemeralWindow] CloseWindow: Window already closing exception: {ex.Message}");
        }
    }

    private void RestoreRevitFocus() {
        try {
            // Get the main Revit window handle
            var revitProcess = Process.GetCurrentProcess();
            var revitHandle = revitProcess.MainWindowHandle;

            // Debug.WriteLine($"[EphemeralWindow] RestoreRevitFocus: Process={revitProcess.ProcessName}, Handle={revitHandle}");

            if (revitHandle != IntPtr.Zero) {
                var revitTitle = this.GetWindowTitle(revitHandle);
                // Debug.WriteLine($"[EphemeralWindow] RestoreRevitFocus: Revit window title='{revitTitle}'");

                var success = SetForegroundWindow(revitHandle);
                // Debug.WriteLine($"[EphemeralWindow] RestoreRevitFocus: SetForegroundWindow returned {success}");

                // Verify focus was restored
                var currentForeground = GetForegroundWindow();
                // Debug.WriteLine($"[EphemeralWindow] RestoreRevitFocus: Current foreground window={currentForeground} (expected {revitHandle})");
            }
            // Debug.WriteLine("[EphemeralWindow] RestoreRevitFocus: Revit handle is zero, cannot restore focus");
        } catch {
            // Debug.WriteLine($"[EphemeralWindow] RestoreRevitFocus: Exception: {ex.Message}");
            // Debug.WriteLine($"[EphemeralWindow] RestoreRevitFocus: StackTrace: {ex.StackTrace}");
        }
    }

    protected override void OnClosing(CancelEventArgs e) {
        this._isClosing = true;
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

                // Get window info for debugging
                var newWindowTitle = this.GetWindowTitle(newActiveWindow);
                var revitWindowTitle = this.GetWindowTitle(revitHandle);
                var foregroundWindowTitle = this.GetWindowTitle(actualForegroundWindow);

                // Debug.WriteLine($"[EphemeralWindow] WM_ACTIVATE: NewActiveWindow={newActiveWindow} ({newWindowTitle})");
                // Debug.WriteLine($"[EphemeralWindow] WM_ACTIVATE: ActualForegroundWindow={actualForegroundWindow} ({foregroundWindowTitle})");
                // Debug.WriteLine($"[EphemeralWindow] WM_ACTIVATE: RevitWindow={revitHandle} ({revitWindowTitle})");
                // Debug.WriteLine($"[EphemeralWindow] WM_ACTIVATE: Alt+Tab active={isAltTabActive}");

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
///     Interface for UserControls that can request their parent window to close.
/// </summary>
public interface ICloseRequestable {
    event EventHandler CloseRequested;
}