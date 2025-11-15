#nullable enable

using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = Wpf.Ui.Controls.MenuItem;

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
            Placement = PlacementMode.Right,
            FocusVisualStyle = null,

        };

        this.Menu.Closed += (_, _) => this.OnExitRequested();
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
        this.RebuildMenu();

        this.Menu.PlacementTarget = placementTarget;
        this.Menu.IsOpen = true;

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

        var actionsList = this._actions.Cast<PaletteAction<TItem>>().ToList();

        for (var i = 0; i < actionsList.Count; i++) {
            var paletteAction = actionsList[i];
            var canExecute = this._currentItem == null || paletteAction.CanExecute(this._currentItem);
            var shortcutText = this.FormatShortcut(paletteAction);

            var menuItem = new MenuItem {
                Header = paletteAction.Name,
                InputGestureText = shortcutText,
                IsEnabled = canExecute,
                FontFamily = ThemeManager.FontFamily(),
                FontSize = (double)TxtSz.normal
            };

            menuItem.Click += (_, _) => {
                this.ActionClicked?.Invoke(this, paletteAction);
                this.Hide();
            };

            _ = this.Menu.Items.Add(menuItem);

            // Add separator between items (but not after the last item)
            if (i < actionsList.Count - 1) {
                var separator = new Separator();
                _ = this.Menu.Items.Add(separator);
            }
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