using AddinPaletteSuite.Core.Actions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Theme = AddinPaletteSuite.Core.Ui.ThemeManager;


namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Non-generic base class for SelectablePalette.xaml
///     This matches the XAML x:Class declaration and provides access to XAML-defined controls
/// </summary>
public partial class SelectablePalette : UserControl, ICloseRequestable {
    public event EventHandler CloseRequested;

    protected void RequestClose() => this.CloseRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>
///     Generic SelectablePalette implementation with typed item support
/// </summary>
public class SelectablePalette<TItem> : SelectablePalette where TItem : BaseObservableListItem, IPaletteListItem {
    private readonly ActionBinding<TItem> _actionBinding;
    private readonly ActionMenu<TItem> _actionMenu;

    public SelectablePalette(
        SelectablePaletteViewModel<TItem> viewModel,
        IEnumerable<PaletteAction<TItem>> actions
    ) {
        // Set DataContext BEFORE InitializeComponent so bindings work
        this.DataContext = viewModel;

        // Now initialize XAML components with DataContext already set
        this.InitializeComponent();

        this._actionBinding = new ActionBinding<TItem>();
        this._actionBinding.RegisterRange(actions);
        this._actionMenu = new ActionMenu<TItem>();

        // Wire up event handlers
        this.Loaded += this.UserControl_Loaded;
        this.KeyDown += this.UserControl_KeyDown;
        this.PreviewKeyDown += this.UserControl_PreviewKeyDown;
        this.SearchTextBox.PreviewKeyDown += this.SearchTextBox_PreviewKeyDown;

        // Initialize theme and apply styles
        Theme.Initialize();
        this.ApplyStyles();
    }

    private SelectablePaletteViewModel<TItem> ViewModel => this.DataContext as SelectablePaletteViewModel<TItem>;

    private void ApplyStyles() {
        this.ApplyMainContainerStyles();
        this.ApplySearchBoxStyles();
        this.ApplyStatusBarStyles();
    }

    private void ApplyMainContainerStyles() {
        this.MainBorder.Background = Theme.BackgroundFillColorPrimaryBrush;
        this.MainBorder.BorderBrush = Theme.ControlStrokeColorDefaultBrush;
        this.MainBorder.BorderThickness = Theme.BorderThicknessThin;
        this.MainBorder.CornerRadius = Theme.CornerRadiusLarge;
        this.MainBorder.Padding = new Thickness(4, 0, 4, 0);

        this.SearchTextBox.FontFamily = Theme.FontFamily;
        this.SearchTextBox.FontSize = Theme.FontSizeNormal;
        this.SearchTextBox.Foreground = Theme.TextFillColorPrimaryBrush;
    }

    private void ApplySearchBoxStyles() {
 
        this.SearchTextBox.FontSize = Theme.FontSizeMedium;
        this.SearchTextBox.Padding = Theme.PaddingMedium;
        this.SearchTextBox.Margin = new Thickness(0);
        this.SearchTextBox.Background = Brushes.Transparent;
        this.SearchTextBox.BorderThickness = Theme.BorderThicknessNone;
        this.SearchTextBox.CaretBrush = Theme.TextFillColorPrimaryBrush;
    }

    private void ApplyStatusBarStyles() {
        this.StatusBarGrid.Margin = Theme.PaddingMedium;
        
        this.ItemCountText.Foreground = Theme.TextFillColorTertiaryBrush;
        this.ItemCountText.Margin = new Thickness(0, 0, 12, 0);

        // Help text
        this.HelpText.FontFamily = Theme.FontFamily;
        this.HelpText.FontSize = Theme.FontSizeNormal;
        this.HelpText.Foreground = Theme.TextFillColorTertiaryBrush;
    }

    private void UpdateCanExecuteForAllItems(SelectablePaletteViewModel<TItem> viewModel) {
        foreach (var item in viewModel.FilteredItems) {
            var firstAction = this._actionBinding.GetAvailableActions(item).FirstOrDefault();
            if (firstAction != null) item.CanExecute = firstAction.CanExecute(item);
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e) {
        if (this.ViewModel == null) throw new InvalidOperationException("SelectablePalette view-model is null");

        _ = this.SearchTextBox.Focus();
        this.SearchTextBox.SelectAll();

        this.ItemListBox.ItemMouseLeftButtonUp += async (_, e) => {
            if (e.OriginalSource is not FrameworkElement source) return;

            var item = source.DataContext as TItem;
            if (item == null) {
                // Try to find the ListBoxItem parent
                var parent = source.Parent as FrameworkElement;
                while (parent is not null and not ListBoxItem) parent = parent.Parent as FrameworkElement;
                if (parent is ListBoxItem listBoxItem) item = listBoxItem.DataContext as TItem;
            }

            if (item == null) return;


            // Update selection to the clicked item
            if (this.ViewModel != null) this.ViewModel.SelectedItem = item;

            var executed = await this._actionBinding.TryExecuteAsync(
                item,
                ModifierKeys.None
            );


            if (executed) {
                this.ViewModel?.RecordUsage();

                this.RequestClose();
            }
        };

        this.ItemListBox.SelectionChanged += (_, e) => {
            if (this.ViewModel.SelectedItem != null) this.ItemListBox.ScrollIntoView(this.ViewModel.SelectedItem);

            // Close popovers when selection changes
            this.HideActionsPopover();
            this.HideTooltipPopover();
        };

        // Set up action menu handlers
        this._actionMenu.ExitRequested += (_, _) => this.HideActionsPopover();
        this._actionMenu.ReturnFocusTarget = this.SearchTextBox;
        this._actionMenu.ActionClicked += this.ActionMenu_ActionClicked;

        // Set up tooltip popover exit handler
        this.TooltipPanel.ExitRequested += (_, _) => this.HideTooltipPopover();
        this.TooltipPanel.ReturnFocusTarget = this.SearchTextBox;
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e) {
        // Don't handle keys if focus is in a popover - let the popover handle its own keys
        if (Keyboard.FocusedElement is DependencyObject focusedElement) {
            if (this.TooltipPanel != null && this.TooltipPanel.IsAncestorOf(focusedElement)) {
            }
        }
    }

    private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
        // Handle Left arrow key to show tooltip popover
        if (e.Key == Key.Left) {
            if (this.ViewModel?.SelectedItem != null) {
                this.UpdateCanExecuteForAllItems(this.DataContext as SelectablePaletteViewModel<TItem>);
                this.PositionTooltipPopover();
                this.TooltipPopup.IsOpen = true;
                this.TooltipPanel.UpdateLayout();
                _ = this.Dispatcher.BeginInvoke(new Action(() => {
                    var tooltipText = this.ViewModel.SelectedItem?.TextInfo;
                    if (tooltipText != null) this.TooltipPanel.Text = tooltipText;
                }), DispatcherPriority.Loaded);
                e.Handled = true;
            }

            return;
        }

        // Handle Right arrow key to show actions popover
        if (e.Key == Key.Right) {
            if (this.ViewModel?.SelectedItem != null) {
                var actions = this._actionBinding.GetAllActions().ToList();
                if (actions.Count > 0) {
                    this.ItemListBox.ScrollIntoView(this.ViewModel.SelectedItem);
                    this.ItemListBox.UpdateLayout();
                    this.UpdateCanExecuteForAllItems(this.DataContext as SelectablePaletteViewModel<TItem>);
                    var selectedItem = this.ViewModel.SelectedItem;
                    var freshListBoxItem =
                        this.ItemListBox.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListBoxItem;
                    _ = this.Dispatcher.BeginInvoke(new Action(() => {
                        if (freshListBoxItem != null) {
                            this._actionMenu.Actions = actions;
                            this._actionMenu.Show(freshListBoxItem, selectedItem);
                        }
                    }), DispatcherPriority.Loaded);
                }

                e.Handled = true;
            }
        }
    }

    private async void UserControl_KeyDown(object sender, KeyEventArgs e) {
        if (this.ViewModel == null) throw new InvalidOperationException("SelectablePalette view-model is null");

        // Don't handle keys if focus is in a popover - let the popover handle its own keys
        if (Keyboard.FocusedElement is DependencyObject focusedElement) {
            if (this.TooltipPanel != null && this.TooltipPanel.IsAncestorOf(focusedElement))
                return; // Let tooltip popover handle its keys
        }

        var selectedItem = this.ViewModel.SelectedItem;

        switch (e.Key) {
        case Key.Escape:
            // If no popover is open, close the palette
            this.RequestClose();
            e.Handled = true;
            break;

        case Key.Enter:

            if (selectedItem != null) {
                var executed = await this._actionBinding.TryExecuteAsync(
                    selectedItem, Key.Enter, ModifierKeys.None);

                if (executed) {
                    this.ViewModel.RecordUsage();
                    this.RequestClose();
                }
            }

            e.Handled = true;
            break;

        case Key.Left:
            if (selectedItem != null) this.PositionTooltipPopover();
            this.TooltipPopup.IsOpen = true;
            this.TooltipPanel.UpdateLayout();
            e.Handled = this.ShowPopover(() => {
                var tooltipText = selectedItem.TextInfo;
                this.TooltipPanel.Text = tooltipText;
            });
            break;

        case Key.Right:
            if (selectedItem != null) {
                e.Handled = this.ShowPopover(() => {
                    var actions = this._actionBinding.GetAllActions().ToList();
                    this._actionMenu.Actions = actions;
                    this._actionMenu.Show(selectedItem as UIElement);
                });
            }

            break;

        case Key.Tab: // Prevent tab from changing focus
            e.Handled = true;
            break;
        }
    }

    private bool ShowPopover(Action action) {
        var selectedItem = this.ViewModel?.SelectedItem;
        if (selectedItem == null) return false;
        this.ItemListBox.ScrollIntoView(this.ViewModel.SelectedItem);
        this.ItemListBox.UpdateLayout();
        _ = this.Dispatcher.BeginInvoke(new Action(() => action()), DispatcherPriority.Loaded);
        return true;
    }

    private void HideActionsPopover() => this._actionMenu.Hide();

    private void HideTooltipPopover() => this.TooltipPopup.IsOpen = false;

    private void PositionTooltipPopover() {
        if (this.ViewModel?.SelectedItem == null) return;

        var listBoxItem =
            this.ItemListBox.ItemContainerGenerator.ContainerFromItem(this.ViewModel.SelectedItem) as ListBoxItem;
        if (listBoxItem == null) {
            // Try to generate container if it doesn't exist yet
            this.ItemListBox.UpdateLayout();
            listBoxItem =
                this.ItemListBox.ItemContainerGenerator.ContainerFromItem(this.ViewModel.SelectedItem) as ListBoxItem;
            if (listBoxItem == null) return;
        }

        // Position popover to the left of the selected item
        this.TooltipPopup.PlacementTarget = listBoxItem;
        this.TooltipPopup.Placement = PlacementMode.Left;
        this.TooltipPopup.HorizontalOffset = 0;
        this.TooltipPopup.VerticalOffset = 0;
    }

    private async void ActionMenu_ActionClicked(object _, PaletteAction<TItem> action) {
        if (this.ViewModel?.SelectedItem == null) return;
        await this._actionBinding.ExecuteActionAsync(action, this.ViewModel.SelectedItem);
        this.ViewModel.RecordUsage();
        this.HideActionsPopover();
        this.RequestClose();
    }
}