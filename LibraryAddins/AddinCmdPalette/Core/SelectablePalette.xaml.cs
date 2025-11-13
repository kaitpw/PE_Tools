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
    private bool _isPopoverOpen;

    public SelectablePalette(
        SelectablePaletteViewModel viewModel,
        IEnumerable<PaletteAction> actions
    ) {
        this.InitializeComponent();
        this.DataContext = viewModel;

        this._actionBinding = new ActionBinding();
        this._actionBinding.RegisterRange(actions);
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

            // Close popover when selection changes
            if (this._isPopoverOpen) this.HidePopover();
        };

        this.InfoPopover.ActionClicked += this.InfoPopover_ActionClicked;
        this.InfoPopup.Closed += (_, _) => this._isPopoverOpen = false;
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e) {
        // Don't handle arrow keys if focus is in the tooltip RichTextBox
        if (Keyboard.FocusedElement is DependencyObject focusedElement) {
            if (this.TooltipPanel != null && this.TooltipPanel.IsAncestorOf(focusedElement)) {
                // Allow arrow keys to work normally in RichTextBox
                if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down) {
                }
            }
        }
    }

    private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
        // Handle Left arrow key to focus tooltip panel
        if (e.Key == Key.Left) {
            this.TooltipPanel.FocusTooltip();
            e.Handled = true;
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

        // Don't handle arrow keys if focus is in the tooltip RichTextBox
        if (Keyboard.FocusedElement is DependencyObject focusedElement) {
            if (this.TooltipPanel != null && this.TooltipPanel.IsAncestorOf(focusedElement)) {
                if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
                    return;
            }
        }


        switch (e.Key) {
        case Key.Escape:

            if (this._isPopoverOpen) {
                this.HidePopover();
                e.Handled = true;
            } else {
                this.RequestClose();
                e.Handled = true;
            }

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

        this.InfoPopover.TooltipText = null;
        this.InfoPopover.Actions = actions;
        this.PositionPopover();
        this.InfoPopup.IsOpen = true;
        this._isPopoverOpen = true;
    }

    private void HidePopover() {
        this.InfoPopup.IsOpen = false;
        this._isPopoverOpen = false;
    }

    private void PositionPopover() {
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

        // Position popover to the right of the selected item
        this.InfoPopup.PlacementTarget = listBoxItem;
        this.InfoPopup.Placement = PlacementMode.Right;
        this.InfoPopup.HorizontalOffset = 0;
        this.InfoPopup.VerticalOffset = 0;
    }

    private async void InfoPopover_ActionClicked(object _, PaletteAction action) {
        if (this.ViewModel?.SelectedItem == null) return;


        try {
            await this._actionBinding.ExecuteActionAsync(action, this.ViewModel.SelectedItem);
            this.ViewModel.RecordUsage();
            this.HidePopover();
            this.RequestClose();
        } catch (Exception ex) {
            this.HidePopover();
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