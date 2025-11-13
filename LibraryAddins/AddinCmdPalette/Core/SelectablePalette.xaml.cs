using AddinCmdPalette.Actions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AddinCmdPalette.Core;

/// <summary>
///     Interaction logic for SelectablePalette.xaml
/// </summary>
public partial class SelectablePalette : UserControl, ICloseRequestable {
    private readonly ActionBinding _actionBinding;
    private readonly ActionMenu _actionMenu;

    public SelectablePalette(
        SelectablePaletteViewModel viewModel,
        IEnumerable<PaletteAction> actions
    ) {
        this.InitializeComponent();
        this.DataContext = viewModel;

        this._actionBinding = new ActionBinding();
        this._actionBinding.RegisterRange(actions);

        this._actionMenu = new ActionMenu();
    }

    private SelectablePaletteViewModel ViewModel => this.DataContext as SelectablePaletteViewModel;

    public event EventHandler CloseRequested;

    private void UserControl_Loaded(object sender, RoutedEventArgs e) {
        if (this.ViewModel == null) throw new InvalidOperationException("SelectablePalette view-model is null");

        _ = this.SearchTextBox.Focus();
        this.SearchTextBox.SelectAll();

        this.ItemListBox.ItemMouseLeftButtonUp += async (_, e) => {
            if (e.OriginalSource is not FrameworkElement source) return;

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

            // Execute action
            try {
                var executed = await this._actionBinding.TryExecuteAsync(
                    item,
                    MouseButton.Left,
                    ModifierKeys.None
                );


                if (executed) {
                    this.ViewModel?.RecordUsage();

                    this.RequestClose();
                }
            } catch (Exception ex) {
                this.RequestClose();
                _ = MessageBox.Show(
                    ex.Message,
                    "Action Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
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
                return; // Let tooltip popover handle its keys
            }
        }
    }

    private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
        // Handle Left arrow key to show tooltip popover
        if (e.Key == Key.Left) {
            if (this.ViewModel?.SelectedItem != null) {
                this.ShowTooltipPopover();
                e.Handled = true;
            }
            return;
        }

        // Handle Right arrow key to show actions popover
        if (e.Key == Key.Right) {
            if (this.ViewModel?.SelectedItem != null) {
                this.ShowActionsPopover();
                e.Handled = true;
            }
        }
    }

    private async void UserControl_KeyDown(object sender, KeyEventArgs e) {
        if (this.ViewModel == null) throw new InvalidOperationException("SelectablePalette view-model is null");

        // Don't handle keys if focus is in a popover - let the popover handle its own keys
        if (Keyboard.FocusedElement is DependencyObject focusedElement) {
            if (this.TooltipPanel != null && this.TooltipPanel.IsAncestorOf(focusedElement)) {
                return; // Let tooltip popover handle its keys
            }
        }

        switch (e.Key) {
        case Key.Escape:
            // If no popover is open, close the palette
            this.RequestClose();
            e.Handled = true;
            break;

        case Key.Enter:

            if (this.ViewModel.SelectedItem != null) {
                try {
                    var executed = await this._actionBinding.TryExecuteAsync(
                        this.ViewModel.SelectedItem,
                        Key.Enter,
                        ModifierKeys.None
                    );


                    if (executed) {
                        this.ViewModel.RecordUsage();

                        this.RequestClose();
                    }
                } catch (Exception ex) {
                    this.RequestClose();
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
            if (this.ViewModel.SelectedItem != null) {
                this.ShowTooltipPopover();
                e.Handled = true;
            }
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
        if (actions.Count == 0) return;

        // Ensure the item is in view first
        this.ItemListBox.ScrollIntoView(this.ViewModel.SelectedItem);

        // Force complete layout pass
        this.ItemListBox.UpdateLayout();

        var listBoxItem =
            this.ItemListBox.ItemContainerGenerator.ContainerFromItem(this.ViewModel.SelectedItem) as ListBoxItem;
        if (listBoxItem == null) return;

        // Wait for layout to complete before showing menu
        _ = this.Dispatcher.BeginInvoke(new Action(() => {
            // Get fresh container reference after layout completes
            var freshListBoxItem =
                this.ItemListBox.ItemContainerGenerator.ContainerFromItem(this.ViewModel.SelectedItem) as ListBoxItem;
            if (freshListBoxItem == null) return;

            this._actionMenu.Actions = actions;
            this._actionMenu.Show(freshListBoxItem);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void HideActionsPopover() => this._actionMenu.Hide();

    private void ShowTooltipPopover() {
        if (this.ViewModel?.SelectedItem == null) return;
        if (string.IsNullOrEmpty(this.ViewModel.SelectedItem.TooltipText)) return;

        // Update tooltip text binding - force update
        var tooltipText = this.ViewModel.SelectedItem.TooltipText;
        this.TooltipPanel.TooltipText = tooltipText;

        Debug.WriteLine($"[Palette] Showing tooltip popover with text: '{tooltipText}'");

        this.PositionTooltipPopover();
        this.TooltipPopup.IsOpen = true;

        // Force update after popup opens
        this.TooltipPanel.UpdateLayout();

        // Focus the tooltip after a short delay to allow popup to render
        _ = this.Dispatcher.BeginInvoke(new Action(() => {
            this.TooltipPanel.TooltipText = tooltipText; // Force update again
            this.TooltipPanel.FocusTooltip();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

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

    private async void ActionMenu_ActionClicked(object _, PaletteAction action) {
        if (this.ViewModel?.SelectedItem == null) return;

        try {
            await this._actionBinding.ExecuteActionAsync(action, this.ViewModel.SelectedItem);
            this.ViewModel.RecordUsage();
            this.HideActionsPopover();
            this.RequestClose();
        } catch (Exception ex) {
            this.HideActionsPopover();
            this.RequestClose();
            _ = MessageBox.Show(
                ex.Message,
                "Action Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void RequestClose() => this.CloseRequested?.Invoke(this, EventArgs.Empty);
}