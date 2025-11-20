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
///     Non-generic base class for SelectablePalette.xaml
///     This matches the XAML x:Class declaration and provides access to XAML-defined controls
/// </summary>
public partial class Palette : RevitHostedUserControl, ICloseRequestable {
    /// <summary>
    ///     Attached property to store ActionBinding for child controls to access
    /// </summary>
    public static readonly DependencyProperty ActionBindingProperty = DependencyProperty.RegisterAttached(
        "ActionBinding",
        typeof(object),
        typeof(Palette),
        new PropertyMetadata(null));

    protected Palette(object dataContext = null) {
        // Set DataContext before InitializeComponent so bindings work
        if (dataContext != null)
            this.DataContext = dataContext;
        this.InitializeComponent();
    }

    public event EventHandler<CloseRequestedEventArgs> CloseRequested;

    public static void SetActionBinding(DependencyObject element, object value) =>
        element.SetValue(ActionBindingProperty, value);

    public static object GetActionBinding(DependencyObject element) =>
        element.GetValue(ActionBindingProperty);

    protected void RequestClose(bool restoreFocus = true) =>
        this.CloseRequested?.Invoke(this, new CloseRequestedEventArgs { RestoreFocus = restoreFocus });

    // Note: SearchBoxBorder, MainBorder, StatusBarBorder, ItemListView, StatusBarBorder
    // are defined in the XAML and accessible via the partial class generated code
}

/// <summary>
///     Generic SelectablePalette implementation with typed item support
/// </summary>
public class Palette<TItem> : Palette where TItem : class, IPaletteListItem {
    private readonly ActionBinding<TItem> _actionBinding;
    private readonly ActionMenu<TItem> _actionMenu;
    private readonly CustomKeyBindings? _customKeyBindings;
    private readonly FilterBox<PaletteViewModel<TItem>>? _filterBox;
    private readonly SelectableTextBox _tooltipPanel;
    private bool _isSearchBoxHidden;

    public Palette(
        PaletteViewModel<TItem> viewModel,
        IEnumerable<PaletteAction<TItem>> actions,
        CustomKeyBindings? customKeyBindings = null
    ) : base(viewModel) {
        this._customKeyBindings = customKeyBindings;
        // Base class constructor sets DataContext and calls InitializeComponent()

        // Load resources for SearchTextBox
        ThemeManager.LoadWpfUiResources(this.SearchTextBox);

        // Create FilterBox if filtering is enabled
        var hasFiltering = viewModel.AvailableFilterValues != null;
        if (hasFiltering) {
            this._filterBox = new FilterBox<PaletteViewModel<TItem>>(viewModel, new[] { Key.Tab, Key.Escape });
            this._filterBox.BindToViewModel("AvailableFilterValues", "SelectedFilterValue");
            this._filterBox.ExitRequested += (_, _) => _ = this.SearchTextBox.Focus();

            Grid.SetColumn(this._filterBox, 1);
            _ = this.SearchBoxGrid.Children.Add(this._filterBox);
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

        this._actionBinding = new ActionBinding<TItem>();
        this._actionBinding.RegisterRange(actions);
        this._actionMenu = new ActionMenu<TItem>([Key.Escape, Key.Left]);

        // Store ActionBinding as attached property so child controls can access it
        SetActionBinding(this, this._actionBinding);

        // Create tooltip panel programmatically
        this._tooltipPanel = new SelectableTextBox([Key.Escape, Key.Up, Key.Down, Key.Right]);

        // Wire up event handlers
        this.Loaded += this.UserControl_Loaded;
        this.PreviewKeyDown += this.UserControl_PreviewKeyDown;
    }


    private PaletteViewModel<TItem> ViewModel => this.DataContext as PaletteViewModel<TItem>;

    /// <summary>
    ///     Executes the selected item with the given modifiers
    /// </summary>
    private async Task<bool> ExecuteItem(
        TItem selectedItem,
        ModifierKeys modifiers = ModifierKeys.None,
        Key key = Key.Enter
    ) {
        var result = await this._actionBinding.TryExecuteAsync(
            selectedItem, key, modifiers);

        if (result.Success) {
            this.ViewModel.RecordUsage();
            this.RequestClose(!result.IsNextPalette);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Hides the search box and sets up alternative focus handling for keyboard-only navigation
    /// </summary>
    public void HideSearchBox() {
        this._isSearchBoxHidden = true;
        this.SearchBoxBorder.Visibility = Visibility.Collapsed;

        // Make the UserControl itself focusable so it can receive keyboard input
        this.Focusable = true;
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e) {
        if (this.ViewModel == null) throw new InvalidOperationException("SelectablePalette view-model is null");

        // If search box is hidden - focus on the UserControl itself to receive keyboard input
        if (this._isSearchBoxHidden)
            _ = this.Focus();
        else {
            _ = this.SearchTextBox.Focus();
            this.SearchTextBox.SelectAll();
        }

        this.ItemListView.ItemMouseLeftButtonUp += async (_, e) => {
            if (e.OriginalSource is not FrameworkElement source) return;
            var item = source.DataContext as TItem;
            if (this.ViewModel != null) this.ViewModel.SelectedItem = item;
            _ = await this.ExecuteItem(item, Keyboard.Modifiers);
        };

        this.ItemListView.ItemMouseRightButtonUp += (_, e) => {
            if (e.OriginalSource is not FrameworkElement source) return;
            var item = source.DataContext as TItem;
            if (item == null) return;
            if (this.ViewModel != null) this.ViewModel.SelectedItem = item;

            e.Handled = this.ShowPopover(placementTarget => {
                this._actionMenu.Actions = this._actionBinding.GetAllActions().ToList();
                this._actionMenu.Show(placementTarget, item);
            });
        };

        // Track last shown tooltip item to prevent flickering
        TItem lastTooltipItem = null;

        this.ItemListView.ItemMouseMove += (_, e) => {
            if (e.OriginalSource is not FrameworkElement source) return;
            var item = source.DataContext as TItem;

            // Hide tooltip if mouse moved away from items
            if (item == null) {
                lastTooltipItem = null;
                this._tooltipPanel.Hide();
                return;
            }

            // Skip if no tooltip text or already showing for this item
            if (string.IsNullOrEmpty(item.TextInfo) || item == lastTooltipItem) return;

            // Update ViewModel selection and show tooltip
            if (this.ViewModel != null) this.ViewModel.SelectedItem = item;
            _ = this.ShowPopover(placementTarget => {
                lastTooltipItem = item;
                this._tooltipPanel.Show(placementTarget, item.TextInfo);
            });
        };

        this.ItemListView.ItemMouseLeave += (_, e) => {
            lastTooltipItem = null;
            this._tooltipPanel.Hide();
        };

        this.ItemListView.SelectionChanged += (_, e) => {
            if (this.ViewModel.SelectedItem != null) this.ItemListView.ScrollIntoView(this.ViewModel.SelectedItem);
        };

        // Set up action menu handlers
        this._actionMenu.ExitRequested += (_, _) => this.Focus();
        this._actionMenu.ActionClicked += this.ActionMenu_ActionClicked;

        // Set up tooltip popover exit handler
        this._tooltipPanel.ExitRequested += (_, _) => this.Focus();
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
            var selectedItem = this.ViewModel.SelectedItem;

            // Check custom key bindings first (and handle no search box palettes)
            if (this._customKeyBindings != null &&
                this._customKeyBindings.TryGetAction(e.Key, modifiers, out var navAction))
                e.Handled = await this.HandleNavigationAction(navAction);
            else if (e.Key == Key.Escape) {
                this.RequestClose();
                e.Handled = true;
            } else if (e.Key == Key.Enter && selectedItem != null)
                e.Handled = await this.ExecuteItem(selectedItem, modifiers);
            else if (e.Key == Key.Tab && modifiers == ModifierKeys.None && this._filterBox != null)
                e.Handled = this.ShowPopover(_ => this._filterBox?.Show());
            else if (e.Key == Key.Left && selectedItem != null) {
                e.Handled = this.ShowPopover(placementTarget =>
                    this._tooltipPanel.Show(placementTarget, selectedItem.TextInfo));
            } else if (e.Key == Key.Right && selectedItem != null) {
                e.Handled = this.ShowPopover(placementTarget => {
                    this._actionMenu.Actions = this._actionBinding.GetAllActions().ToList();
                    this._actionMenu.Show(placementTarget, selectedItem);
                });
            }
        } catch { }
    }

    private bool ShowPopover(Action<UIElement> action) {
        var selectedItem = this.ViewModel?.SelectedItem;
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
        if (this.ViewModel == null) return false;

        switch (action) {
        case NavigationAction.MoveUp:
            this.ViewModel.MoveSelectionUpCommand.Execute(null);
            return true;

        case NavigationAction.MoveDown:
            this.ViewModel.MoveSelectionDownCommand.Execute(null);
            return true;

        case NavigationAction.Execute:
            return await this.ExecuteItem(this.ViewModel.SelectedItem);

        case NavigationAction.Cancel:
            this.RequestClose();
            return true;

        default:
            return false;
        }
    }

    private async void ActionMenu_ActionClicked(object _, PaletteAction<TItem> action) {
        if (this.ViewModel?.SelectedItem == null) return;
        var isNextPalette = await this._actionBinding.ExecuteActionAsync(action, this.ViewModel.SelectedItem);
        this.ViewModel.RecordUsage();
        this.RequestClose(!isNextPalette);
    }
}