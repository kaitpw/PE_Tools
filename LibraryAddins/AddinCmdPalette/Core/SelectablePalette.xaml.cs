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

    public SelectablePalette(
        SelectablePaletteViewModel viewModel,
        IEnumerable<PaletteAction> actions
    ) {
        this.InitializeComponent();
        this.LoadResources();
        this.DataContext = viewModel;

        this._actionBinding = new ActionBinding();
        this._actionBinding.RegisterRange(actions);

        this.Loaded += this.OnLoad;
        this.Deactivated += (_, _) => {
            if (!this.IsActive && !this._isClosing && !this.IsMouseOver) this.CloseWindow();
        };
        this.LostFocus += (_, _) => {
            if (!this._isClosing) this.CloseWindow();
        };
    }

    private void LoadResources() {
        try {
            Debug.WriteLine("[SelectablePalette] Loading WPF-UI resources from WpfUiResources.xaml");

            // Load WPF-UI resources (includes ThemesDictionary, ControlsDictionary, and custom overrides)
            // Try multiple pack URI formats
            var resourcePaths = new[] {
                "pack://application:,,,/PE_Tools;component/LibraryAddins/AddinCmdPalette/Core/WpfUiResources.xaml",
                "pack://application:,,,/PE_Tools;component/WpfUiResources.xaml",
                "/LibraryAddins/AddinCmdPalette/Core/WpfUiResources.xaml",
                "/WpfUiResources.xaml"
            };

            ResourceDictionary resourceDict = null;
            Exception lastException = null;

            foreach (var path in resourcePaths) {
                try {
                    Debug.WriteLine($"[SelectablePalette] Trying resource path: {path}");
                    var uri = new Uri(path, UriKind.RelativeOrAbsolute);
                    resourceDict = (ResourceDictionary)Application.LoadComponent(uri);
                    Debug.WriteLine($"[SelectablePalette] Successfully loaded resource from: {path}");
                    break;
                } catch (Exception ex) {
                    Debug.WriteLine($"[SelectablePalette] Failed to load from {path}: {ex.Message}");
                    lastException = ex;
                }
            }

            if (resourceDict != null) {
                this.Resources.MergedDictionaries.Add(resourceDict);
                Debug.WriteLine("[SelectablePalette] WPF-UI resources merged successfully");
            } else {
                Debug.WriteLine($"[SelectablePalette] Failed to load WPF-UI resources. Last error: {lastException?.Message}");
                // Fallback: create basic resources inline
                this.CreateFallbackResources();
            }
        } catch (Exception ex) {
            Debug.WriteLine($"[SelectablePalette] Error loading resources: {ex}");
            this.CreateFallbackResources();
        }
    }

    private void CreateFallbackResources() {
        Debug.WriteLine("[SelectablePalette] Creating fallback resources inline");
        var fallbackDict = new ResourceDictionary();

        // Custom Shadcn-inspired dark palette colors
        fallbackDict["BackgroundFillColorPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#18181B"));
        fallbackDict["BackgroundFillColorSecondaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1F1F23"));
        fallbackDict["BackgroundFillColorTertiaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27272A"));
        fallbackDict["TextFillColorPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FAFAFA"));
        fallbackDict["TextFillColorSecondaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A1A1AA"));
        fallbackDict["TextFillColorTertiaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#71717A"));
        fallbackDict["ControlFillColorDefaultBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27272A"));
        fallbackDict["ControlStrokeColorDefaultBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3F3F46"));

        this.Resources.MergedDictionaries.Add(fallbackDict);
        Debug.WriteLine("[SelectablePalette] Fallback resources created and merged");
    }

    private SelectablePaletteViewModel ViewModel => this.DataContext as SelectablePaletteViewModel;

    private void OnLoad(object sender, RoutedEventArgs eventArgs) {
        if (this.ViewModel == null) throw new InvalidOperationException("SelectablePalette view-model is null");

        this.ItemListBox.MouseLeftButtonUp += async (_, e) => {
            // Get the clicked item from the event source
            if (e.OriginalSource is FrameworkElement source) {
                var item = source.DataContext as ISelectableItem;
                if (item == null) {
                    // Try to find the ListBoxItem parent
                    var parent = source.Parent as FrameworkElement;
                    while (parent is not null and not ListBoxItem) parent = parent.Parent as FrameworkElement;
                    if (parent is ListBoxItem listBoxItem) item = listBoxItem.DataContext as ISelectableItem;
                }

                if (item == null) return;

                // Update selection to the clicked item
                if (this.ViewModel != null) this.ViewModel.SelectedItem = item;

                // Only execute actions with no modifiers directly
                try {
                    var executed = await this._actionBinding.TryExecuteAsync(
                        item,
                        MouseButton.Left,
                        ModifierKeys.None
                    );

                    if (executed) {
                        this.ViewModel?.RecordUsage();
                        this.CloseWindow();
                    }
                } catch (Exception ex) {
                    this.CloseWindow();
                    _ = MessageBox.Show(
                        ex.Message,
                        "Action Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
            }
        };

        this.ItemListBox.SelectionChanged += (_, _) => {
            if (this.ViewModel.SelectedItem != null)
                this.ItemListBox.ScrollIntoView(this.ViewModel.SelectedItem);

            // Close popover when selection changes
            if (this._isPopoverOpen) this.HidePopover();
        };

        this.InfoPopover.ActionClicked += this.InfoPopover_ActionClicked;
        this.InfoPopup.Closed += (_, _) => this._isPopoverOpen = false;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) {
        _ = this.SearchTextBox.Focus();
        this.SearchTextBox.SelectAll();
    }

    private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
        Debug.WriteLine($"[SelectablePalette] SearchTextBox_PreviewKeyDown: {e.Key}");

        // Handle Left/Right arrow keys here to prevent TextBox from consuming them
        if (e.Key == Key.Left) {
            Debug.WriteLine("[SelectablePalette] Left arrow in PreviewKeyDown");
            if (this.ViewModel?.SelectedItem != null) {
                Debug.WriteLine("[SelectablePalette] Showing tooltip popover from PreviewKeyDown");
                this.ShowTooltipPopover();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Right) {
            Debug.WriteLine("[SelectablePalette] Right arrow in PreviewKeyDown");
            if (this.ViewModel?.SelectedItem != null) {
                Debug.WriteLine("[SelectablePalette] Showing actions popover from PreviewKeyDown");
                this.ShowActionsPopover();
                e.Handled = true;
            }
            return;
        }
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e) {
        Debug.WriteLine($"[SelectablePalette] KeyDown: {e.Key}, Handled: {e.Handled}, Source: {e.Source}");

        if (this.ViewModel == null) throw new InvalidOperationException("SelectablePalette view-model is null");
        if (this._isClosing) {
            Debug.WriteLine("[SelectablePalette] Window is closing, ignoring key");
            return;
        }

        switch (e.Key) {
        case Key.Escape:
            Debug.WriteLine("[SelectablePalette] Escape key pressed, popover open: " + this._isPopoverOpen);
            if (this._isPopoverOpen) {
                this.HidePopover();
                e.Handled = true;
            } else {
                this.CloseWindow();
                e.Handled = true;
            }
            break;

        case Key.Enter:
            Debug.WriteLine($"[SelectablePalette] Enter key pressed, selected item: {this.ViewModel.SelectedItem?.PrimaryText ?? "null"}");
            if (this.ViewModel.SelectedItem != null) {
                // Only execute actions with no modifiers directly
                try {
                    var executed = await this._actionBinding.TryExecuteAsync(
                        this.ViewModel.SelectedItem,
                        Key.Enter,
                        ModifierKeys.None
                    );

                    if (executed) {
                        this.ViewModel.RecordUsage();
                        this.CloseWindow();
                    }
                } catch (Exception ex) {
                    this.CloseWindow();
                    _ = MessageBox.Show(
                        ex.Message,
                        "Action Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
            }

            e.Handled = true;
            break;

        case Key.Left:
            Debug.WriteLine($"[SelectablePalette] Left arrow pressed, selected item: {this.ViewModel.SelectedItem?.PrimaryText ?? "null"}");
            if (this.ViewModel.SelectedItem != null) {
                Debug.WriteLine("[SelectablePalette] Showing tooltip popover");
                this.ShowTooltipPopover();
                e.Handled = true;
            } else {
                Debug.WriteLine("[SelectablePalette] No selected item, cannot show tooltip");
            }
            break;

        case Key.Right:
            Debug.WriteLine($"[SelectablePalette] Right arrow pressed, selected item: {this.ViewModel.SelectedItem?.PrimaryText ?? "null"}");
            if (this.ViewModel.SelectedItem != null) {
                Debug.WriteLine("[SelectablePalette] Showing actions popover");
                this.ShowActionsPopover();
                e.Handled = true;
            } else {
                Debug.WriteLine("[SelectablePalette] No selected item, cannot show actions");
            }
            break;

        case Key.Tab: // Prevent tab from changing focus
            e.Handled = true;
            break;
        }
    }

    private void ShowTooltipPopover() {
        Debug.WriteLine("[SelectablePalette] ShowTooltipPopover called");
        if (this.ViewModel?.SelectedItem == null) {
            Debug.WriteLine("[SelectablePalette] ShowTooltipPopover: No selected item");
            return;
        }

        Debug.WriteLine($"[SelectablePalette] ShowTooltipPopover: Tooltip text = {this.ViewModel.SelectedItem.TooltipText}");
        this.InfoPopover.TooltipText = this.ViewModel.SelectedItem.TooltipText;
        this.InfoPopover.Actions = null;
        this.PositionPopover();
        this.InfoPopup.IsOpen = true;
        this._isPopoverOpen = true;
        Debug.WriteLine($"[SelectablePalette] ShowTooltipPopover: Popup.IsOpen = {this.InfoPopup.IsOpen}");
    }

    private void ShowActionsPopover() {
        Debug.WriteLine("[SelectablePalette] ShowActionsPopover called");
        if (this.ViewModel?.SelectedItem == null) {
            Debug.WriteLine("[SelectablePalette] ShowActionsPopover: No selected item");
            return;
        }

        var actions = this._actionBinding.GetAvailableActions(this.ViewModel.SelectedItem).ToList();
        Debug.WriteLine($"[SelectablePalette] ShowActionsPopover: Found {actions.Count} actions");
        this.InfoPopover.TooltipText = null;
        this.InfoPopover.Actions = actions;
        this.PositionPopover();
        this.InfoPopup.IsOpen = true;
        this._isPopoverOpen = true;
        Debug.WriteLine($"[SelectablePalette] ShowActionsPopover: Popup.IsOpen = {this.InfoPopup.IsOpen}");
    }

    private void HidePopover() {
        this.InfoPopup.IsOpen = false;
        this._isPopoverOpen = false;
    }

    private void PositionPopover() {
        Debug.WriteLine($"[SelectablePalette] PositionPopover: Window position = ({this.Left}, {this.Top}), Size = ({this.Width}, {this.Height})");

        if (this.ViewModel?.SelectedItem == null) {
            Debug.WriteLine("[SelectablePalette] PositionPopover: No selected item");
            return;
        }

        var listBoxItem = this.ItemListBox.ItemContainerGenerator.ContainerFromItem(this.ViewModel.SelectedItem) as ListBoxItem;
        if (listBoxItem == null) {
            // Try to generate container if it doesn't exist yet
            this.ItemListBox.UpdateLayout();
            listBoxItem = this.ItemListBox.ItemContainerGenerator.ContainerFromItem(this.ViewModel.SelectedItem) as ListBoxItem;
            if (listBoxItem == null) {
                Debug.WriteLine("[SelectablePalette] PositionPopover: Could not find ListBoxItem container");
                return;
            }
        }

        // Calculate position relative to the window to determine best placement
        try {
            var transform = listBoxItem.TransformToAncestor(this);
            var rect = new Rect(0, 0, listBoxItem.RenderSize.Width, listBoxItem.RenderSize.Height);
            var bounds = transform.TransformBounds(rect);

            // Estimate popover width (use MaxWidth from InfoPopover or default)
            var estimatedPopoverWidth = this.InfoPopover.MaxWidth > 0 ? this.InfoPopover.MaxWidth : 400;
            var spaceOnRight = this.Width - bounds.Right;
            var spaceOnLeft = bounds.Left;

            Debug.WriteLine($"[SelectablePalette] PositionPopover: Item bounds = ({bounds.Left}, {bounds.Top}, {bounds.Width}, {bounds.Height})");
            Debug.WriteLine($"[SelectablePalette] PositionPopover: Space on right = {spaceOnRight}, Space on left = {spaceOnLeft}, Estimated popover width = {estimatedPopoverWidth}");

            // Use PlacementTarget for simpler positioning
            this.InfoPopup.PlacementTarget = listBoxItem;

            // Choose placement based on available space
            if (spaceOnRight >= estimatedPopoverWidth || spaceOnRight > spaceOnLeft) {
                // Enough space on right, or right has more space
                this.InfoPopup.Placement = PlacementMode.Right;
                this.InfoPopup.HorizontalOffset = 8;
                Debug.WriteLine("[SelectablePalette] PositionPopover: Using right placement");
            } else {
                // Not enough space on right, use left
                this.InfoPopup.Placement = PlacementMode.Left;
                this.InfoPopup.HorizontalOffset = -8;
                Debug.WriteLine("[SelectablePalette] PositionPopover: Using left placement");
            }

            this.InfoPopup.VerticalOffset = 0;
        } catch (Exception ex) {
            Debug.WriteLine($"[SelectablePalette] PositionPopover: Error calculating position: {ex.Message}");
            // Fallback to right placement
            this.InfoPopup.PlacementTarget = listBoxItem;
            this.InfoPopup.Placement = PlacementMode.Right;
            this.InfoPopup.HorizontalOffset = 8;
            this.InfoPopup.VerticalOffset = 0;
        }
    }

    private async void InfoPopover_ActionClicked(object sender, PaletteAction action) {
        if (this.ViewModel?.SelectedItem == null) return;

        try {
            await this._actionBinding.ExecuteActionAsync(action, this.ViewModel.SelectedItem);
            this.ViewModel.RecordUsage();
            this.HidePopover();
            this.CloseWindow();
        } catch (Exception ex) {
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

    private void CloseWindow() {
        try {
            if (this._isClosing) return;
            this._isClosing = true;
            this.HidePopover();
            this.Close();
        } catch (InvalidOperationException) { } // Window is already closing, ignore
    }

    protected override void OnClosing(CancelEventArgs e) {
        this._isClosing = true;
        base.OnClosing(e);
    }

    #region Hiding from Alt+Tab

    protected override void OnSourceInitialized(EventArgs e) {
        base.OnSourceInitialized(e);
        // Remove window from Alt+Tab
        var helper = new WindowInteropHelper(this);
        _ = SetWindowLong(
            helper.Handle,
            GWL_EXSTYLE,
            GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_TOOLWINDOW
        );
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    #endregion
}