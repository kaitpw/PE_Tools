using AddinPaletteSuite.Core.Actions;
using System.Collections;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Wpf.Ui.Controls;

#nullable enable

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Context menu component for displaying available actions with arrow key navigation
/// </summary>
public class ActionMenu : IPopoverExit
{
    private readonly System.Windows.Controls.ContextMenu _contextMenu;
    private IEnumerable? _actions;

    public ActionMenu()
    {
        this._contextMenu = new System.Windows.Controls.ContextMenu
        {
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

    public event EventHandler? ExitRequested;
    public event EventHandler<PaletteAction>? ActionClicked;

    public UIElement? ReturnFocusTarget { get; set; }

    public void RequestExit()
    {
        this._contextMenu.IsOpen = false;
        _ = this.ReturnFocusTarget?.Focus();
    }

    public IEnumerable? Actions
    {
        get => this._actions;
        set
        {
            this._actions = value;
            this.RebuildMenu();
        }
    }

    /// <summary>
    ///     Shows the action menu positioned to the right of the target element
    /// </summary>
    public void Show(UIElement placementTarget)
    {
        if (this._actions == null) return;

        this._contextMenu.PlacementTarget = placementTarget;
        this._contextMenu.IsOpen = true;

        // Focus the first menu item after menu opens
        _ = this._contextMenu.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (this._contextMenu.Items.Count > 0 && this._contextMenu.Items[0] is Wpf.Ui.Controls.MenuItem firstItem)
                _ = firstItem.Focus();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    ///     Hides the action menu
    /// </summary>
    public void Hide() => this._contextMenu.IsOpen = false;

    private void RebuildMenu()
    {
        this._contextMenu.Items.Clear();

        if (this._actions == null) return;

        foreach (var action in this._actions)
        {
            if (action is not PaletteAction paletteAction) continue;

            var menuItem = new Wpf.Ui.Controls.MenuItem
            {
                Header = paletteAction.Name,
                InputGestureText = this.FormatShortcut(paletteAction)
            };

            menuItem.Click += (_, _) =>
            {
                this.ActionClicked?.Invoke(this, paletteAction);
                this.Hide();
            };

            _ = this._contextMenu.Items.Add(menuItem);
        }
    }

    private void ContextMenu_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
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

    private string FormatShortcut(PaletteAction action)
    {
        var parts = new List<string>();

        if (action.Modifiers != ModifierKeys.None)
        {
            if ((action.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((action.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((action.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        }

        if (action.Key.HasValue)
        {
            var keyStr = action.Key.Value.ToString();
            if (keyStr == "Return") keyStr = "Enter";
            parts.Add(keyStr);
        }

        if (action.MouseButton.HasValue)
        {
            var buttonStr = action.MouseButton.Value switch
            {
                MouseButton.Left => "Click",
                MouseButton.Right => "Right-Click",
                MouseButton.Middle => "Middle-Click",
                MouseButton.XButton1 => "X1-Click",
                MouseButton.XButton2 => "X2-Click",
                _ => action.MouseButton.Value.ToString()
            };
            parts.Add(buttonStr);
        }

        return parts.Count > 0 ? string.Join("+", parts) : string.Empty;
    }
}

