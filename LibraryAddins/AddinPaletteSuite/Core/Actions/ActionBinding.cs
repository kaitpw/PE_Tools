using AddinPaletteSuite.Core;
using AddinPaletteSuite.Core.Ui;
using System.Windows.Input;

namespace AddinPaletteSuite.Core.Actions;

/// <summary>
///     Manages action registration and execution for palette items
/// </summary>
public class ActionBinding {
    private readonly List<PaletteAction> _actions = new();

    /// <summary>
    ///     Registers an action with the binding system
    /// </summary>
    public void Register(PaletteAction action) => this._actions.Add(action);

    /// <summary>
    ///     Registers multiple actions
    /// </summary>
    public void RegisterRange(IEnumerable<PaletteAction> actions) => this._actions.AddRange(actions);

    /// <summary>
    ///     Finds and executes the matching action for a keyboard event
    /// </summary>
    public async Task<bool> TryExecuteAsync(IPaletteListItem item, Key key, ModifierKeys modifiers) {
        var action = this.FindMatchingAction(key, modifiers, null);
        if (action == null || !action.CanExecute(item)) return false;

        await this.ExecuteActionInternalAsync(action, item);
        return true;
    }

    /// <summary>
    ///     Finds and executes the matching action for a mouse event
    /// </summary>
    public async Task<bool> TryExecuteAsync(IPaletteListItem item, MouseButton button, ModifierKeys modifiers) {
        var action = this.FindMatchingAction(null, modifiers, button);
        if (action == null || !action.CanExecute(item)) return false;

        await this.ExecuteActionInternalAsync(action, item);
        return true;
    }

    /// <summary>
    ///     Gets all available actions for a given item (filtered by CanExecute)
    /// </summary>
    public IEnumerable<PaletteAction> GetAvailableActions(IPaletteListItem item) =>
        this._actions.Where(a => a.CanExecute(item));

    /// <summary>
    ///     Executes a specific action for a given item
    /// </summary>
    public async Task ExecuteActionAsync(PaletteAction action, IPaletteListItem item) {
        if (!action.CanExecute(item))
            throw new InvalidOperationException($"Action '{action.Name}' cannot execute for this item");

        await this.ExecuteActionInternalAsync(action, item);
    }

    /// <summary>
    ///     Internal helper that executes either synchronous or asynchronous action
    /// </summary>
    private async Task ExecuteActionInternalAsync(PaletteAction action, IPaletteListItem item) {
        if (action.ExecuteAsync != null) {
            await action.ExecuteAsync(item);
        } else if (action.Execute != null) {
            action.Execute(item);
        } else {
            throw new InvalidOperationException($"Action '{action.Name}' has neither Execute nor ExecuteAsync defined");
        }
    }

    /// <summary>
    ///     Finds the best matching action for the given input combination
    /// </summary>
#nullable enable
    private PaletteAction? FindMatchingAction(Key? key, ModifierKeys modifiers, MouseButton? mouseButton) {
#nullable disable
        // Find exact matches first (most specific)
        var exactMatch = this._actions.FirstOrDefault(a =>
            a.Modifiers == modifiers &&
            ((key.HasValue && a.Key == key) || (!key.HasValue && a.Key == null)) &&
            ((mouseButton.HasValue && a.MouseButton == mouseButton) ||
             (!mouseButton.HasValue && a.MouseButton == null)));

        if (exactMatch != null) return exactMatch;

        // Fall back to default action (no modifiers, no specific key/button)
        return this._actions.FirstOrDefault(a =>
            a.Modifiers == ModifierKeys.None &&
            a.Key == null &&
            a.MouseButton == null);
    }
}