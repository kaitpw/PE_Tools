#nullable enable

using System.Collections;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Context menu component for displaying available actions with arrow key navigation
/// </summary>
public class ActionMenu<TItem> : IPopoverExit where TItem : BaseObservableListItem, IPaletteListItem {
    private readonly ContextMenu _contextMenu;
    private IEnumerable? _actions;
    private TItem? _currentItem;

    public ActionMenu() {
        this._contextMenu = new ContextMenu {
            StaysOpen = false,
            PlacementTarget = null,
            Placement = PlacementMode.Right,
            HorizontalOffset = 0,
            VerticalOffset = 0
        };

        // Handle context menu closing to raise exit event
        this._contextMenu.Closed += (_, _) => this.ExitRequested?.Invoke(this, EventArgs.Empty);

        // Add keyboard handler for Left arrow and Escape
        this._contextMenu.PreviewKeyDown += this.ContextMenu_PreviewKeyDown;
    }

    public IEnumerable? Actions {
        get => this._actions;
        set {
            this._actions = value;
            this.RebuildMenu();
        }
    }

    public event EventHandler? ExitRequested;

    public UIElement? ReturnFocusTarget { get; set; }

    public void RequestExit() {
        this._contextMenu.IsOpen = false;
        _ = this.ReturnFocusTarget?.Focus();
    }

    public event EventHandler<PaletteAction<TItem>>? ActionClicked;

    /// <summary>
    ///     Shows the action menu positioned to the right of the target element
    /// </summary>
    public void Show(UIElement placementTarget, TItem? currentItem = null) {
        if (this._actions == null) return;

        this._currentItem = currentItem;
        this.RebuildMenu(); // Rebuild to update enabled/disabled state

        this._contextMenu.PlacementTarget = placementTarget;
        this._contextMenu.IsOpen = true;

        // Focus the first enabled menu item after menu opens
        _ = this._contextMenu.Dispatcher.BeginInvoke(new Action(() => {
            var firstEnabledItem = this._contextMenu.Items.OfType<MenuItem>()
                .FirstOrDefault(mi => mi.IsEnabled);
            if (firstEnabledItem != null)
                _ = firstEnabledItem.Focus();
        }), DispatcherPriority.Loaded);
    }

    /// <summary>
    ///     Hides the action menu
    /// </summary>
    public void Hide() => this._contextMenu.IsOpen = false;

    private void RebuildMenu() {
        this._contextMenu.Items.Clear();

        if (this._actions == null) return;

        foreach (var action in this._actions) {
            if (action is not PaletteAction<TItem> paletteAction) continue;

            // Check if action can execute for the current item
            var canExecute = this._currentItem == null || paletteAction.CanExecute(this._currentItem);

            var menuItem = new MenuItem {
                Header = paletteAction.Name,
                InputGestureText = this.FormatShortcut(paletteAction),
                IsEnabled = canExecute,
                Opacity = canExecute ? 1.0 : 0.4
            };

            menuItem.Click += (_, _) => {
                this.ActionClicked?.Invoke(this, paletteAction);
                this.Hide();
            };

            _ = this._contextMenu.Items.Add(menuItem);
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