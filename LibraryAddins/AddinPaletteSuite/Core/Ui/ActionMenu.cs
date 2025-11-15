#nullable enable

using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Grid = System.Windows.Controls.Grid;
using MenuItem = Wpf.Ui.Controls.MenuItem;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using Theme = AddinPaletteSuite.Core.Ui.ThemeManager;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Non-generic base class for ActionMenu.xaml
///     This matches the XAML x:Class declaration and provides access to resources
/// </summary>
public partial class ActionMenu : UserControl, IPopoverExit {
    protected ActionMenu() => this.InitializeComponent();
    protected ContextMenu? Menu { get; set; }
    public event EventHandler? ExitRequested;
    public UIElement? ReturnFocusTarget { get; set; }

    public virtual void RequestExit() {
        if (this.Menu != null) this.Menu.IsOpen = false;
        _ = this.ReturnFocusTarget?.Focus();
    }

    protected virtual void OnExitRequested() => this.ExitRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>
///     Generic ActionMenu implementation with typed item support
///     Context menu component for displaying available actions with arrow key navigation
/// </summary>
public class ActionMenu<TItem> : ActionMenu where TItem : BaseObservableListItem, IPaletteListItem {
    private IEnumerable? _actions;
    private TItem? _currentItem;

    public ActionMenu() {
        this.Menu = new ContextMenu {
            StaysOpen = false,
            PlacementTarget = null,
            Placement = PlacementMode.Right,
            HorizontalOffset = 0,
            VerticalOffset = 0,
            Background = Theme.PrimaryBg(),
            BorderBrush = Theme.SecondaryHi(),
            BorderThickness = new Thickness((double)UiSz.ss),
            FocusVisualStyle = null,
            Style = (Style)this.FindResource("ActionMenuStyle"),
            Padding = new Thickness(0)
        };

        // Handle context menu closing to raise exit event
        this.Menu.Closed += (_, _) => this.OnExitRequested();

        // Add keyboard handler for Left arrow and Escape
        this.Menu.PreviewKeyDown += this.ContextMenu_PreviewKeyDown;
    }

    public IEnumerable? Actions {
        get => this._actions;
        set {
            this._actions = value;
            this.RebuildMenu();
        }
    }

    public override void RequestExit() => base.RequestExit();

    public event EventHandler<PaletteAction<TItem>>? ActionClicked;

    /// <summary>
    ///     Shows the action menu positioned to the right of the target element
    /// </summary>
    public void Show(UIElement placementTarget, TItem? currentItem = null) {
        if (this._actions == null || this.Menu == null) return;

        this._currentItem = currentItem;
        this.RebuildMenu(); // Rebuild to update enabled/disabled state

        this.Menu.PlacementTarget = placementTarget;
        this.Menu.IsOpen = true;

        // Focus the first enabled menu item after menu opens
        _ = this.Menu.Dispatcher.BeginInvoke(new Action(() => {
            var firstEnabledItem = this.Menu!.Items.OfType<MenuItem>()
                .FirstOrDefault(mi => mi.IsEnabled);
            if (firstEnabledItem != null)
                _ = firstEnabledItem.Focus();
        }), DispatcherPriority.Loaded);
    }

    /// <summary>
    ///     Hides the action menu
    /// </summary>
    public void Hide() {
        if (this.Menu != null) this.Menu.IsOpen = false;
    }

    private void RebuildMenu() {
        if (this.Menu == null) return;

        this.Menu.Items.Clear();

        if (this._actions == null) return;

        var isFirst = true;
        foreach (var action in this._actions) {
            if (action is not PaletteAction<TItem> paletteAction) continue;

            // Add separator before each item except the first
            if (!isFirst) {
                var separator = new Separator {
                    BorderThickness = new Thickness((double)UiSz.ss),
                    BorderBrush = Theme.PrimaryTxt(),
                };
                _ = this.Menu.Items.Add(separator);
            }
            isFirst = false;

            // Check if action can execute for the current item
            var canExecute = this._currentItem == null || paletteAction.CanExecute(this._currentItem);

            // Create custom header content with full control over layout
            var headerBorder = new Border().WithPadding(UiSz.l, UiSz.m, UiSz.l, UiSz.l);
            headerBorder.CornerRadius = new CornerRadius((double)UiSz.m);
            headerBorder.Margin = new Thickness(0);

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Action name TextBlock
            var nameTextBlock = Theme.StyleTextBlock(new TextBlock {
                Text = paletteAction.Name,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            });
            Grid.SetColumn(nameTextBlock, 0);

            // Shortcut TextBlock
            var shortcutText = this.FormatShortcut(paletteAction);
            var shortcutTextBlock =
                Theme.StyleTextBlock(
                    new TextBlock { Text = shortcutText, VerticalAlignment = VerticalAlignment.Center }, TxtSz.s,
                    false);
            Grid.SetColumn(shortcutTextBlock, 1);

            _ = headerGrid.Children.Add(nameTextBlock);
            if (!string.IsNullOrEmpty(shortcutText)) _ = headerGrid.Children.Add(shortcutTextBlock);

            headerBorder.Child = headerGrid;

            // Create MenuItem with custom header
            var menuItem = new MenuItem {
                Header = headerBorder,
                IsEnabled = canExecute,
                Opacity = canExecute ? Theme.ItemOpacityEnabled : Theme.ItemOpacityDisabled,
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = Brushes.Transparent,
                Style = (Style)this.FindResource("ActionMenuItemStyle"),
                Width = 110
            };
            // Set highlight color on mouse enter and focus
            menuItem.MouseEnter += (_, _) => {
                if (menuItem.IsEnabled) headerBorder.Background = Theme.SecondaryHi();
            };
            menuItem.MouseLeave += (_, _) => headerBorder.Background = Brushes.Transparent;
            menuItem.GotFocus += (_, _) => {
                if (menuItem.IsEnabled) headerBorder.Background = Theme.SecondaryHi();
            };
            menuItem.LostFocus += (_, _) => headerBorder.Background = Brushes.Transparent;

            menuItem.Click += (_, _) => {
                this.ActionClicked?.Invoke(this, paletteAction);
                this.Hide();
            };

            _ = this.Menu!.Items.Add(menuItem);
        }
    }

    private void ContextMenu_PreviewKeyDown(object sender, KeyEventArgs e) {
        switch (e.Key) {
        case Key.Escape:
            e.Handled = true;
            this.RequestExit();
            break;
        case Key.Left:
            e.Handled = true;
            this.RequestExit();
            break;
        }
    }

    private string FormatShortcut(PaletteAction<TItem> action) {
        var parts = new List<string>();

        if (action.Modifiers != ModifierKeys.None) {
            if ((action.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((action.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((action.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        }

        if (action.Key.HasValue) {
            var keyStr = action.Key.Value.ToString();
            if (keyStr == "Return") keyStr = "Enter";
            parts.Add(keyStr);
        }

        return parts.Count > 0 ? string.Join("+", parts) : string.Empty;
    }
}