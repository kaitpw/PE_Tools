using PeUi.Core;
using PeUi.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Visibility = System.Windows.Visibility;
using Grid = System.Windows.Controls.Grid;


namespace PeUi.Components;

/// <summary>
///     Attached property holder and common close behavior for Palette.
///     Separated from XAML class to support generic usage.
/// </summary>
public static class PaletteAttachedProperties {
    /// <summary>
    ///     Attached property to store ActionBinding for child controls to access
    /// </summary>
    public static readonly DependencyProperty ActionBindingProperty = DependencyProperty.RegisterAttached(
        "ActionBinding",
        typeof(object),
        typeof(PaletteAttachedProperties),
        new PropertyMetadata(null));

    public static void SetActionBinding(DependencyObject element, object value) =>
        element.SetValue(ActionBindingProperty, value);

    public static object GetActionBinding(DependencyObject element) =>
        element.GetValue(ActionBindingProperty);
}

/// <summary>
///     XAML-backed Palette component. This is the ONLY class that should be used
///     with the Palette.xaml file. Generic behavior is handled via composition,
///     NOT inheritance (generic classes cannot inherit from XAML partial classes).
/// </summary>
public sealed partial class Palette : RevitHostedUserControl, ICloseRequestable {
    private readonly bool _isSearchBoxHidden;
    private ActionBinding _actionBinding;
    private ActionMenu _actionMenu;
    private CustomKeyBindings _customKeyBindings;
    private Func<Task<bool>> _executeItemFunc;
    private FilterBox _filterBox;
    private Func<object> _getSelectedItemFunc;
    private bool _isCtrlPressed;
    private Action _onCtrlReleased;
    private Action _recordUsageFunc; // TODO: this probably exists from my refactors, did i mess something up?
    private SelectableTextBox _tooltipPanel;

    public Palette(bool isSearchBoxHidden = false) {
        this.InitializeComponent();
        if (isSearchBoxHidden) {
            this._isSearchBoxHidden = true;
            this.SearchBoxBorder.Visibility = Visibility.Collapsed;
            // Make the UserControl itself focusable so it can receive keyboard input
            this.Focusable = true;
        }
    }

    public event EventHandler<CloseRequestedEventArgs> CloseRequested;

    /// <summary>
    ///     Initializes the palette with type-specific behavior via composition.
    ///     This must be called after construction to wire up generic-specific logic.
    /// </summary>
    internal void Initialize<TItem>(
        PaletteViewModel<TItem> viewModel,
        IEnumerable<PaletteAction<TItem>> actions,
        CustomKeyBindings customKeyBindings = null,
        Action onCtrlReleased = null
    ) where TItem : class, IPaletteListItem {
        this.DataContext = viewModel;
        this._customKeyBindings = customKeyBindings;

        // Load resources for SearchTextBox
        ThemeManager.LoadWpfUiResources(this.SearchTextBox);

        // Create FilterBox if filtering is enabled
        var hasFiltering = viewModel.AvailableFilterValues != null;
        if (hasFiltering) {
            var filterBox = new FilterBox<PaletteViewModel<TItem>>(
                viewModel,
                [Key.Tab, Key.Escape],
                viewModel.AvailableFilterValues
            );
            filterBox.ExitRequested += (_, _) => _ = this.SearchTextBox.Focus();
            this._filterBox = filterBox;

            Grid.SetColumn(filterBox, 1);
            _ = this.SearchBoxGrid.Children.Add(filterBox);
        }

        new BorderSpec()
            .Border()
            .ApplyToBorder(this.MainBorder);
        this.MainBorder.ClipToBounds = true;

        new BorderSpec()
            .Border((UiSz.l, UiSz.l, UiSz.none, UiSz.none))
            .Padding(UiSz.ll, UiSz.ll, UiSz.ll, UiSz.ll)
            .ApplyToBorder(this.SearchBoxBorder);

        new BorderSpec()
            .Border((UiSz.none, UiSz.none, UiSz.l, UiSz.l))
            .Padding(UiSz.l, UiSz.s, UiSz.l, UiSz.s)
            .ApplyToBorder(this.StatusBarBorder);
        this.StatusBarBorder.ClipToBounds = true;

        var actionBinding = new ActionBinding<TItem>();
        if (actions != null && actions.Any()) actionBinding.RegisterRange(actions);
        var actionMenu = new ActionMenu<TItem>([Key.Escape, Key.Left]);

        // Store type-erased references for non-generic code paths
        this._actionBinding = actionBinding;
        this._actionMenu = actionMenu;

        // Store ActionBinding as attached property so child controls can access it
        PaletteAttachedProperties.SetActionBinding(this, actionBinding);

        // Create tooltip panel programmatically
        this._tooltipPanel = new SelectableTextBox([Key.Escape, Key.Up, Key.Down, Key.Right]);

        // Capture typed delegates for use in non-generic handlers
        this._getSelectedItemFunc = () => viewModel.SelectedItem;
        this._recordUsageFunc = viewModel.RecordUsage;
        this._executeItemFunc = async () => {
            var selectedItem = viewModel.SelectedItem;
            if (selectedItem == null) return false;
            return await this.ExecuteItemTyped(selectedItem, actionBinding, viewModel, Keyboard.Modifiers);
        };

        // Store Ctrl-release callback if provided
        this._onCtrlReleased = onCtrlReleased;

        // Wire up typed event handlers
        this.SetupTypedEventHandlers(viewModel, actionBinding, actionMenu);

        // Wire up event handlers
        this.Loaded += this.UserControl_Loaded;
        this.PreviewKeyDown += this.UserControl_PreviewKeyDown;
        this.PreviewKeyUp += this.UserControl_PreviewKeyUp;
    }

    private void SetupTypedEventHandlers<TItem>(
        PaletteViewModel<TItem> viewModel,
        ActionBinding<TItem> actionBinding,
        ActionMenu<TItem> actionMenu
    ) where TItem : class, IPaletteListItem {
        this.ItemListView.ItemMouseLeftButtonUp += async (_, e) => {
            if (e.OriginalSource is not FrameworkElement source) return;
            var item = source.DataContext as TItem;
            if (item == null) return;
            viewModel.SelectedItem = item;
            _ = await this.ExecuteItemTyped(item, actionBinding, viewModel, Keyboard.Modifiers);
        };

        this.ItemListView.ItemMouseRightButtonUp += (_, e) => {
            if (e.OriginalSource is not FrameworkElement source) return;
            var item = source.DataContext as TItem;
            if (item == null) return;
            viewModel.SelectedItem = item;

            e.Handled = this.ShowPopover(placementTarget => {
                actionMenu.Actions = actionBinding.GetAllActions().ToList();
                actionMenu.Show(placementTarget, item);
            });
        };

        this.ItemListView.SelectionChanged += (_, _) => {
            if (viewModel.SelectedItem != null) this.ItemListView.ScrollIntoView(viewModel.SelectedItem);
        };

        // Set up action menu handlers
        actionMenu.ExitRequested += (_, _) => this.Focus();
        actionMenu.ActionClicked += async (_, action) => {
            if (viewModel.SelectedItem == null) return;
            var isNextPalette = await actionBinding.ExecuteActionAsync(action, viewModel.SelectedItem);
            viewModel.RecordUsage();
            this.RequestClose(!isNextPalette);
        };

        // Set up tooltip popover exit handler
        this._tooltipPanel.ExitRequested += (_, _) => this.Focus();
    }

    private async Task<bool> ExecuteItemTyped<TItem>(
        TItem selectedItem,
        ActionBinding<TItem> actionBinding,
        PaletteViewModel<TItem> viewModel,
        ModifierKeys modifiers = ModifierKeys.None,
        Key key = Key.Enter
    ) where TItem : class, IPaletteListItem {
        var result = await actionBinding.TryExecuteAsync(selectedItem, key, modifiers);

        if (result.Success) {
            viewModel.RecordUsage();
            this.RequestClose(!result.IsNextPalette);
            return true;
        }

        return false;
    }

    private void RequestClose(bool restoreFocus = true) =>
        this.CloseRequested?.Invoke(this, new CloseRequestedEventArgs { RestoreFocus = restoreFocus });

    private void UserControl_Loaded(object sender, RoutedEventArgs e) {
        if (this.DataContext == null) throw new InvalidOperationException("Palette DataContext is null");

        // Check if Ctrl is already pressed (e.g., palette opened with Ctrl+`)
        if (this._onCtrlReleased != null)
            this._isCtrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

        // If search box is hidden - focus on the UserControl itself to receive keyboard input
        if (this._isSearchBoxHidden)
            _ = this.Focus();
        else {
            _ = this.SearchTextBox.Focus();
            this.SearchTextBox.SelectAll();
        }
    }

    private async void UserControl_PreviewKeyDown(object sender, KeyEventArgs e) {
        try {
            // Don't handle keys if focus is in a child RevitHostedUserControl (popover or FilterBox)
            if (Keyboard.FocusedElement is not DependencyObject focusedElement) return;

            // Walk up the visual tree to find if focus is inside another RevitHostedUserControl
            var current = focusedElement;
            while (current != null) {
                if (current is RevitHostedUserControl control && control != this)
                    return; // Focus is in a child component, let it handle its own keys
                current = VisualTreeHelper.GetParent(current);
            }

            var modifiers = e.KeyboardDevice.Modifiers;
            var selectedItem = this._getSelectedItemFunc?.Invoke();

            // Track Ctrl key state for Ctrl-release behavior
            if ((modifiers & ModifierKeys.Control) != 0)
                this._isCtrlPressed = true;

            // Check custom key bindings first (and handle no search box palettes)
            if (this._customKeyBindings != null &&
                this._customKeyBindings.TryGetAction(e.Key, modifiers, out var navAction))
                e.Handled = await this.HandleNavigationAction(navAction);
            else if (e.Key == Key.Escape) {
                this.RequestClose();
                e.Handled = true;
            } else if (e.Key == Key.Enter && selectedItem != null)
                e.Handled = await this._executeItemFunc();
            // No idea why this is needed, but it is and its very counterintuitive. 
            // Without it, when the search box is hidden, ONLY the up/down keys work, and none of the others
            else if (e.Key == Key.Up && modifiers == ModifierKeys.None && this._isSearchBoxHidden)
                e.Handled = await this.HandleNavigationAction(NavigationAction.MoveUp);
            else if (e.Key == Key.Down && modifiers == ModifierKeys.None && this._isSearchBoxHidden)
                e.Handled = await this.HandleNavigationAction(NavigationAction.MoveDown);
            else if (e.Key == Key.Tab && modifiers == ModifierKeys.None && this._filterBox != null)
                e.Handled = this.ShowPopover(_ => this._filterBox?.Show());
            else if (e.Key == Key.Left && selectedItem is IPaletteListItem item) {
                e.Handled = this.ShowPopover(placementTarget => {
                    // Lazy evaluate tooltip text only when showing
                    var tooltipText = item.GetTextInfo?.Invoke();
                    this._tooltipPanel.Show(placementTarget, tooltipText);
                });
            } else if (e.Key == Key.Right && selectedItem != null) {
                e.Handled = this.ShowPopover(placementTarget => {
                    this._actionMenu?.SetActionsUntyped(this._actionBinding?.GetAllActionsUntyped());
                    this._actionMenu?.ShowUntyped(placementTarget, selectedItem);
                });
            }
        } catch { }
    }

    private void UserControl_PreviewKeyUp(object sender, KeyEventArgs e) {
        // Handle Ctrl-release behavior
        if (this._onCtrlReleased != null && this._isCtrlPressed) {
            var modifiers = e.KeyboardDevice.Modifiers;
            // Check if Ctrl was released (no longer in modifiers)
            if ((modifiers & ModifierKeys.Control) == 0) {
                this._isCtrlPressed = false;
                this._onCtrlReleased?.Invoke();
                this.RequestClose();
            }
        }
    }

    private bool ShowPopover(Action<UIElement> action) {
        var selectedItem = this._getSelectedItemFunc?.Invoke();
        if (selectedItem == null) return false;
        this.ItemListView.UpdateLayout();
        var container = this.ItemListView.ContainerFromItem(selectedItem);
        if (container == null) return false;
        _ = this.Dispatcher.BeginInvoke(() => action(container), DispatcherPriority.Loaded);
        return true;
    }

    /// <summary>
    ///     Handles custom navigation actions triggered by key bindings
    /// </summary>
    private async Task<bool> HandleNavigationAction(NavigationAction action) {
        if (this.DataContext is not IPaletteViewModel viewModel) return false;

        switch (action) {
        case NavigationAction.MoveUp:
            viewModel.MoveSelectionUpCommand.Execute(null);
            return true;

        case NavigationAction.MoveDown:
            viewModel.MoveSelectionDownCommand.Execute(null);
            return true;

        case NavigationAction.Execute:
            return await this._executeItemFunc();

        case NavigationAction.Cancel:
            this.RequestClose();
            return true;

        default:
            return false;
        }
    }
}