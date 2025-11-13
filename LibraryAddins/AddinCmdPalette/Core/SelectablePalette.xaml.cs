using AddinCmdPalette.Actions;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;

namespace AddinCmdPalette.Core;

/// <summary>
///     Interaction logic for SelectablePalette.xaml
/// </summary>
public partial class SelectablePalette : Window {
    private readonly ActionBinding _actionBinding;
    private bool _isClosing;
    private bool _isPopoverOpen;

    public static readonly DependencyProperty SizeToContentModeProperty = DependencyProperty.Register(
        nameof(SizeToContentMode),
        typeof(SizeToContent),
        typeof(SelectablePalette),
        new PropertyMetadata(SizeToContent.Manual, OnSizeToContentModeChanged));

    public SizeToContent SizeToContentMode {
        get => (SizeToContent)this.GetValue(SizeToContentModeProperty);
        set => this.SetValue(SizeToContentModeProperty, value);
    }

    private static void OnSizeToContentModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is SelectablePalette window) {
            window.SizeToContent = (SizeToContent)e.NewValue;
        }
    }

    public SelectablePalette(
        SelectablePaletteViewModel viewModel,
        IEnumerable<PaletteAction> actions
    ) {
        this.InitializeComponent();
        this.DataContext = viewModel;

        this._actionBinding = new ActionBinding();
        this._actionBinding.RegisterRange(actions);

        this.Loaded += this.OnLoad;

        // Window closing is handled entirely by WndProc (WM_ACTIVATE) for better control
        // Deactivated and LostKeyboardFocus handlers removed to avoid conflicts
    }

    private SelectablePaletteViewModel ViewModel => this.DataContext as SelectablePaletteViewModel;

    private void OnLoad(object sender, RoutedEventArgs eventArgs) {
        if (this.ViewModel == null) throw new InvalidOperationException("SelectablePalette view-model is null");

        this.ItemListBox.ItemMouseLeftButtonUp += async (_, e) => {
            if (e.OriginalSource is not FrameworkElement source) return;

            var item = source.DataContext as ISelectableItem;
            if (item == null) {
                // Try to find the ListBoxItem parent
                var parent = source.Parent as FrameworkElement;
                while (parent is not null and not ListBoxItem) parent = parent.Parent as FrameworkElement;
                if (parent is ListBoxItem listBoxItem) {
                    item = listBoxItem.DataContext as ISelectableItem;
                }
            }

            if (item == null) return;
            Debug.WriteLine($"[Palette] Action: Mouse click on item: {item.PrimaryText}");

            // Update selection to the clicked item
            if (this.ViewModel != null) {
                this.ViewModel.SelectedItem = item;
            }

            // Execute action
            try {
                Debug.WriteLine($"[Palette] Executing action for clicked item: {item.PrimaryText}");
                var executed = await this._actionBinding.TryExecuteAsync(
                    item,
                    MouseButton.Left,
                    ModifierKeys.None
                );

                Debug.WriteLine($"[Palette] Action execution result: {executed}");
                if (executed) {
                    this.ViewModel?.RecordUsage();
                    Debug.WriteLine("[Palette] Action: Mouse click → Close (action executed)");
                    this.CloseWindow();
                }
            } catch (Exception ex) {
                Debug.WriteLine($"[Palette] Action error: {ex.Message}");
                Debug.WriteLine("[Palette] Action: Mouse click → Close (error occurred)");
                this.CloseWindow();
                _ = MessageBox.Show(
                    ex.Message,
                    "Action Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        };

        this.ItemListBox.SelectionChanged += (_, e) => {
            Debug.WriteLine($"[Palette] Selection: {this.ViewModel.SelectedItem?.PrimaryText ?? "null"} (idx={this.ItemListBox.SelectedIndex})");
            if (this.ViewModel.SelectedItem != null) {
                this.ItemListBox.ScrollIntoView(this.ViewModel.SelectedItem);
            }

            // Close popover when selection changes
            if (this._isPopoverOpen) {
                this.HidePopover();
            }
        };

        this.InfoPopover.ActionClicked += this.InfoPopover_ActionClicked;
        this.InfoPopup.Closed += (_, _) => this._isPopoverOpen = false;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) {
        _ = this.SearchTextBox.Focus();
        this.SearchTextBox.SelectAll();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
        // Don't handle arrow keys if focus is in the tooltip RichTextBox
        if (Keyboard.FocusedElement is DependencyObject focusedElement) {
            if (this.TooltipPanel != null && this.TooltipPanel.IsAncestorOf(focusedElement)) {
                // Allow arrow keys to work normally in RichTextBox
                if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down) {
                    Debug.WriteLine($"[Palette] PreviewKey: {e.Key} (in tooltip, pass through)");
                    return;
                }
            }
        }
    }

    private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
        // Handle Left arrow key to focus tooltip panel
        if (e.Key == Key.Left) {
            Debug.WriteLine("[Palette] SearchBox ← → Focus tooltip");
            this.TooltipPanel.FocusTooltip();
            e.Handled = true;
            return;
        }

        // Handle Right arrow key to show actions popover
        if (e.Key == Key.Right) {
            if (this.ViewModel?.SelectedItem != null) {
                Debug.WriteLine("[Palette] SearchBox → Show actions");
                this.ShowActionsPopover();
                e.Handled = true;
            }
            return;
        }
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e) {
        if (this.ViewModel == null) throw new InvalidOperationException("SelectablePalette view-model is null");
        if (this._isClosing) return;

        // Don't handle arrow keys if focus is in the tooltip RichTextBox
        if (Keyboard.FocusedElement is DependencyObject focusedElement) {
            if (this.TooltipPanel != null && this.TooltipPanel.IsAncestorOf(focusedElement)) {
                if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down) {
                    Debug.WriteLine($"[Palette] Key: {e.Key} (in tooltip, skip)");
                    return;
                }
            }
        }

        Debug.WriteLine($"[Palette] Key: {e.Key}");

        switch (e.Key) {
        case Key.Escape:
            Debug.WriteLine("[Palette] Action: Escape key pressed");
            if (this._isPopoverOpen) {
                Debug.WriteLine("[Palette] Popover is open, hiding it");
                this.HidePopover();
                e.Handled = true;
            } else {
                Debug.WriteLine("[Palette] Action: Escape → Close");
                this.CloseWindow();
                e.Handled = true;
            }
            break;

        case Key.Enter:
            Debug.WriteLine("[Palette] Action: Enter key pressed");
            if (this.ViewModel.SelectedItem != null) {
                Debug.WriteLine($"[Palette] Executing action for: {this.ViewModel.SelectedItem.PrimaryText}");
                try {
                    var executed = await this._actionBinding.TryExecuteAsync(
                        this.ViewModel.SelectedItem,
                        Key.Enter,
                        ModifierKeys.None
                    );

                    Debug.WriteLine($"[Palette] Action execution result: {executed}");
                    if (executed) {
                        this.ViewModel.RecordUsage();
                        Debug.WriteLine("[Palette] Action: Enter → Close (action executed)");
                        this.CloseWindow();
                    }
                } catch (Exception ex) {
                    Debug.WriteLine($"[Palette] Action execution error: {ex.Message}");
                    Debug.WriteLine("[Palette] Action: Enter → Close (error occurred)");
                    this.CloseWindow();
                    _ = MessageBox.Show(
                        ex.Message,
                        "Action Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
            } else {
                Debug.WriteLine("[Palette] Enter pressed but no selected item");
            }

            e.Handled = true;
            break;

        case Key.Left:
            this.TooltipPanel.FocusTooltip();
            e.Handled = true;
            break;

        case Key.Right:
            if (this.ViewModel.SelectedItem != null) {
                this.ShowActionsPopover();
                e.Handled = true;
            }
            break;

        case Key.Tab: // Prevent tab from changing focus
            e.Handled = true;
            break;
        }
    }


    private void ShowActionsPopover() {
        if (this.ViewModel?.SelectedItem == null) return;

        var actions = this._actionBinding.GetAvailableActions(this.ViewModel.SelectedItem).ToList();
        Debug.WriteLine($"[Palette] Show popover: {actions.Count} actions");
        this.InfoPopover.TooltipText = null;
        this.InfoPopover.Actions = actions;
        this.PositionPopover();
        this.InfoPopup.IsOpen = true;
        this._isPopoverOpen = true;
    }

    private void HidePopover() {
        Debug.WriteLine("[Palette] Hide popover");
        this.InfoPopup.IsOpen = false;
        this._isPopoverOpen = false;
    }

    private void PositionPopover() {
        if (this.ViewModel?.SelectedItem == null) return;

        var listBoxItem = this.ItemListBox.ItemContainerGenerator.ContainerFromItem(this.ViewModel.SelectedItem) as ListBoxItem;
        if (listBoxItem == null) {
            // Try to generate container if it doesn't exist yet
            this.ItemListBox.UpdateLayout();
            listBoxItem = this.ItemListBox.ItemContainerGenerator.ContainerFromItem(this.ViewModel.SelectedItem) as ListBoxItem;
            if (listBoxItem == null) return;
        }

        // Position popover to the right of the selected item
        this.InfoPopup.PlacementTarget = listBoxItem;
        this.InfoPopup.Placement = PlacementMode.Right;
        this.InfoPopup.HorizontalOffset = 0;
        this.InfoPopup.VerticalOffset = 0;
    }

    private async void InfoPopover_ActionClicked(object sender, PaletteAction action) {
        if (this.ViewModel?.SelectedItem == null) return;

        Debug.WriteLine($"[Palette] Execute action: {action.Name}");
        try {
            await this._actionBinding.ExecuteActionAsync(action, this.ViewModel.SelectedItem);
            this.ViewModel.RecordUsage();
            this.HidePopover();
            this.CloseWindow();
        } catch (Exception ex) {
            Debug.WriteLine($"[Palette] Action error: {ex.Message}");
            this.HidePopover();
            this.CloseWindow();
            _ = MessageBox.Show(
                ex.Message,
                "Action Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void CloseWindow(bool restoreFocus = true) {
        Debug.WriteLine($"[Palette] CloseWindow called: restoreFocus={restoreFocus}, _isClosing={this._isClosing}");
        try {
            if (this._isClosing) {
                Debug.WriteLine("[Palette] CloseWindow: Already closing, aborting");
                return;
            }

            this._isClosing = true;
            Debug.WriteLine("[Palette] CloseWindow: Hiding popover");
            this.HidePopover();

            // Restore focus to Revit before closing (unless user is switching to another app)
            if (restoreFocus) {
                Debug.WriteLine("[Palette] CloseWindow: Restoring focus to Revit");
                this.RestoreRevitFocus();
            } else {
                Debug.WriteLine("[Palette] CloseWindow: Skipping focus restore (user switching apps)");
            }

            Debug.WriteLine("[Palette] CloseWindow: Calling Window.Close()");
            this.Close();
        } catch (InvalidOperationException ex) {
            Debug.WriteLine($"[Palette] CloseWindow: Window already closing exception: {ex.Message}");
        }
    }

    private void RestoreRevitFocus() {
        try {
            // Get the main Revit window handle
            var revitProcess = System.Diagnostics.Process.GetCurrentProcess();
            var revitHandle = revitProcess.MainWindowHandle;

            Debug.WriteLine($"[Palette] RestoreRevitFocus: Process={revitProcess.ProcessName}, Handle={revitHandle}");

            if (revitHandle != IntPtr.Zero) {
                var revitTitle = this.GetWindowTitle(revitHandle);
                Debug.WriteLine($"[Palette] RestoreRevitFocus: Revit window title='{revitTitle}'");

                var success = SetForegroundWindow(revitHandle);
                Debug.WriteLine($"[Palette] RestoreRevitFocus: SetForegroundWindow returned {success}");

                // Verify focus was restored
                var currentForeground = GetForegroundWindow();
                Debug.WriteLine($"[Palette] RestoreRevitFocus: Current foreground window={currentForeground} (expected {revitHandle})");
            } else {
                Debug.WriteLine("[Palette] RestoreRevitFocus: Revit handle is zero, cannot restore focus");
            }
        } catch (Exception ex) {
            Debug.WriteLine($"[Palette] RestoreRevitFocus: Exception: {ex.Message}");
            Debug.WriteLine($"[Palette] RestoreRevitFocus: StackTrace: {ex.StackTrace}");
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
            Debug.WriteLine($"[Palette] WM_ACTIVATE: type={activateType} (0=inactive, 1=active, 2=click)");

            if (activateType == WA_INACTIVE && !this._isClosing) {
                // lParam contains the handle of the window being activated (may be zero)
                var newActiveWindow = lParam;
                var revitHandle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

                // Get actual foreground window (more reliable than lParam)
                var actualForegroundWindow = GetForegroundWindow();

                // Check if Alt key is pressed (Alt+Tab is active)
                var isAltTabActive = (GetAsyncKeyState(0x12) & 0x8000) != 0; // VK_MENU = 0x12

                // Get window info for debugging
                var newWindowTitle = this.GetWindowTitle(newActiveWindow);
                var revitWindowTitle = this.GetWindowTitle(revitHandle);
                var foregroundWindowTitle = this.GetWindowTitle(actualForegroundWindow);

                Debug.WriteLine($"[Palette] WM_ACTIVATE: NewActiveWindow={newActiveWindow} ({newWindowTitle})");
                Debug.WriteLine($"[Palette] WM_ACTIVATE: ActualForegroundWindow={actualForegroundWindow} ({foregroundWindowTitle})");
                Debug.WriteLine($"[Palette] WM_ACTIVATE: RevitWindow={revitHandle} ({revitWindowTitle})");
                Debug.WriteLine($"[Palette] WM_ACTIVATE: Alt+Tab active={isAltTabActive}");

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

                Debug.WriteLine($"[Palette] Action: {actionType} → Close (restoreFocus={shouldRestoreFocus})");

                // Use Dispatcher to avoid issues with closing during message processing
                _ = this.Dispatcher.BeginInvoke(new Action(() => {
                    if (!this._isClosing) {
                        this.CloseWindow(restoreFocus: shouldRestoreFocus);
                    }
                }));
            }
        }

        return IntPtr.Zero;
    }

    private string GetWindowTitle(IntPtr hwnd) {
        if (hwnd == IntPtr.Zero) return "null";

        try {
            const int maxLength = 256;
            var title = new System.Text.StringBuilder(maxLength);
            _ = GetWindowText(hwnd, title, maxLength);
            var titleText = title.ToString();

            if (string.IsNullOrEmpty(titleText)) {
                // Try to get process name instead
                _ = GetWindowThreadProcessId(hwnd, out var processId);
                try {
                    var process = System.Diagnostics.Process.GetProcessById((int)processId);
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
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    #endregion
}