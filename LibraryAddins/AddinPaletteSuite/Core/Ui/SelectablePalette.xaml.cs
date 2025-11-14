using AddinPaletteSuite.Core.Actions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using WpfUiListViewItem = Wpf.Ui.Controls.ListViewItem;
using Theme = AddinPaletteSuite.Core.Ui.ThemeManager;


namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Non-generic base class for SelectablePalette.xaml
///     This matches the XAML x:Class declaration and provides access to XAML-defined controls
/// </summary>
public partial class SelectablePalette : UserControl, ICloseRequestable {
    public event EventHandler<CloseRequestedEventArgs> CloseRequested;

    protected void RequestClose(bool restoreFocus = true) =>
        this.CloseRequested?.Invoke(this, new CloseRequestedEventArgs { RestoreFocus = restoreFocus });
}

/// <summary>
///     Generic SelectablePalette implementation with typed item support
/// </summary>
public class SelectablePalette<TItem> : SelectablePalette where TItem : BaseObservableListItem, IPaletteListItem {
    private readonly ActionBinding<TItem> _actionBinding;
    private readonly ActionMenu<TItem> _actionMenu;
    private readonly SelectableTextBox _tooltipPanel;
    private readonly Popup _tooltipPopup;

    public SelectablePalette(
        SelectablePaletteViewModel<TItem> viewModel,
        IEnumerable<PaletteAction<TItem>> actions
    ) {
        // Set DataContext BEFORE InitializeComponent so bindings work
        this.DataContext = viewModel;

        // Now initialize XAML components with DataContext already set 
        this.InitializeComponent();
        this.ApplyStyles();

        this._actionBinding = new ActionBinding<TItem>();
        this._actionBinding.RegisterRange(actions);
        this._actionMenu = new ActionMenu<TItem>();

        // Create tooltip popup and panel programmatically
        this._tooltipPanel = new SelectableTextBox();
        this._tooltipPopup = new Popup {
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            StaysOpen = false,
            Child = this._tooltipPanel
        };

        // Wire up event handlers
        this.Loaded += this.UserControl_Loaded;
        this.KeyDown += this.UserControl_KeyDown;
        this.PreviewKeyDown += this.UserControl_PreviewKeyDown;
        this.SearchTextBox.PreviewKeyDown += this.SearchTextBox_PreviewKeyDown;
    }

    private SelectablePaletteViewModel<TItem> ViewModel => this.DataContext as SelectablePaletteViewModel<TItem>;

    private void ApplyStyles() {
        _ = this.MainBorder
            .ApplyBorder(bgColor: Theme.PrimaryAccent);
        _ = this.SearchBoxBorder
            .ApplyBorder(bgColor: Theme.PrimaryAccent)
            .WithSpacing(0, 0)
            .WithPadding(UiSz.l, UiSz.m, UiSz.ll, UiSz.m);
        this.SearchTextBox.CaretBrush = Theme.SecondaryTextAccent;
        this.SearchTextBox.FontSize = (double)TxtSz.m;
        // this.SearchTextBox.Font
        _ = this.StatusBarBorder
            .ApplyBorder(bgColor: Theme.SecondaryAccent);

    }

    private void UpdateCanExecuteForVisibleItems() {
        if (this.ViewModel == null) return;

        // Only update CanExecute for items that have generated containers (visible or recently visible)
        foreach (var item in this.ViewModel.FilteredItems) {
            var container = this.ItemListView.ItemContainerGenerator.ContainerFromItem(item);
            if (container != null) {
                var firstAction = this._actionBinding.GetAvailableActions(item).FirstOrDefault();
                if (firstAction != null) item.CanExecute = firstAction.CanExecute(item);
            }
        }
    }

    private void UpdateCanExecuteForSelectedItem() {
        if (this.ViewModel?.SelectedItem == null) return;

        var firstAction = this._actionBinding.GetAvailableActions(this.ViewModel.SelectedItem).FirstOrDefault();
        if (firstAction != null)
            this.ViewModel.SelectedItem.CanExecute = firstAction.CanExecute(this.ViewModel.SelectedItem);
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e) {
        if (this.ViewModel == null) throw new InvalidOperationException("SelectablePalette view-model is null");

        _ = this.SearchTextBox.Focus();
        this.SearchTextBox.SelectAll();

        this.ItemListView.ItemMouseLeftButtonUp += async (_, e) => {
            if (e.OriginalSource is not FrameworkElement source) return;
            var item = source.DataContext as TItem;
            if (this.ViewModel != null) this.ViewModel.SelectedItem = item;
            var result = await this._actionBinding.TryExecuteAsync(
                item, ModifierKeys.None);

            if (result.Success) {
                this.ViewModel?.RecordUsage();
                this.RequestClose(restoreFocus: !result.IsNextPalette);
            }
        };

        this.ItemListView.SelectionChanged += (_, e) => {
            if (this.ViewModel.SelectedItem != null) this.ItemListView.ScrollIntoView(this.ViewModel.SelectedItem);

            // Close popovers when selection changes
            this.HideActionsPopover();
            this.HideTooltipPopover();
        };

        // Set up action menu handlers
        this._actionMenu.ExitRequested += (_, _) => this.HideActionsPopover();
        this._actionMenu.ReturnFocusTarget = this.SearchTextBox;
        this._actionMenu.ActionClicked += this.ActionMenu_ActionClicked;

        // Set up tooltip popover exit handler
        this._tooltipPanel.ExitRequested += (_, _) => this.HideTooltipPopover();
        this._tooltipPanel.ReturnFocusTarget = this.SearchTextBox;
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e) {
        // Don't handle keys if focus is in a popover - let the popover handle its own keys
        if (Keyboard.FocusedElement is DependencyObject focusedElement) {
            if (this._tooltipPanel.IsAncestorOf(focusedElement)) {
            }
        }
    }

    private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
        // Handle Left arrow key to show tooltip popover
        if (e.Key == Key.Left) {
            if (this.ViewModel?.SelectedItem != null) {
                this.UpdateCanExecuteForSelectedItem();
                this.PositionTooltipPopover();
                this._tooltipPopup.IsOpen = true;
                _ = this.Dispatcher.BeginInvoke(new Action(() => {
                    var tooltipText = this.ViewModel.SelectedItem?.TextInfo;
                    this._tooltipPanel.Show(tooltipText);
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
                    this.ItemListView.ScrollIntoView(this.ViewModel.SelectedItem);
                    this.ItemListView.UpdateLayout();
                    this.UpdateCanExecuteForVisibleItems();
                    var selectedItem = this.ViewModel.SelectedItem;
                    var freshListViewItem =
                        this.ItemListView.ItemContainerGenerator.ContainerFromItem(selectedItem) as WpfUiListViewItem;
                    _ = this.Dispatcher.BeginInvoke(new Action(() => {
                        if (freshListViewItem != null) {
                            this._actionMenu.Actions = actions;
                            this._actionMenu.Show(freshListViewItem, selectedItem);
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
            if (this._tooltipPanel.IsAncestorOf(focusedElement))
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
                var result = await this._actionBinding.TryExecuteAsync(
                    selectedItem, Key.Enter, ModifierKeys.None);

                if (result.Success) {
                    this.ViewModel.RecordUsage();
                    this.RequestClose(restoreFocus: !result.IsNextPalette);
                }
            }

            e.Handled = true;
            break;

        case Key.Left:
            if (selectedItem != null) this.PositionTooltipPopover();
            this._tooltipPopup.IsOpen = true;
            e.Handled = this.ShowPopover(() => {
                var tooltipText = selectedItem.TextInfo;
                this._tooltipPanel.Show(tooltipText);
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
        this.ItemListView.ScrollIntoView(this.ViewModel.SelectedItem);
        this.ItemListView.UpdateLayout();
        _ = this.Dispatcher.BeginInvoke(new Action(() => action()), DispatcherPriority.Loaded);
        return true;
    }

    private void HideActionsPopover() => this._actionMenu.Hide();

    private void HideTooltipPopover() {
        this._tooltipPanel.Hide();
        this._tooltipPopup.IsOpen = false;
    }

    private void PositionTooltipPopover() {
        if (this.ViewModel?.SelectedItem == null) return;

        var listViewItem =
            this.ItemListView.ItemContainerGenerator
                .ContainerFromItem(this.ViewModel.SelectedItem) as WpfUiListViewItem;
        if (listViewItem == null) {
            // Try to generate container if it doesn't exist yet
            this.ItemListView.UpdateLayout();
            listViewItem =
                this.ItemListView.ItemContainerGenerator.ContainerFromItem(this.ViewModel.SelectedItem) as
                    WpfUiListViewItem;
            if (listViewItem == null) return;
        }

        // Position popover to the left of the selected item
        this._tooltipPopup.PlacementTarget = listViewItem;
        this._tooltipPopup.Placement = PlacementMode.Left;
        this._tooltipPopup.HorizontalOffset = 0;
        this._tooltipPopup.VerticalOffset = 0;
    }

    private async void ActionMenu_ActionClicked(object _, PaletteAction<TItem> action) {
        if (this.ViewModel?.SelectedItem == null) return;
        var isNextPalette = await this._actionBinding.ExecuteActionAsync(action, this.ViewModel.SelectedItem);
        this.ViewModel.RecordUsage();
        this.HideActionsPopover();
        this.RequestClose(restoreFocus: !isNextPalette);
    }
}