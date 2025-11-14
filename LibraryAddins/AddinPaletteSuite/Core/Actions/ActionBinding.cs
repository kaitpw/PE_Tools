using AddinPaletteSuite.Core;
using AddinPaletteSuite.Core.Ui;
using System.Windows.Input;

namespace AddinPaletteSuite.Core.Actions;

/// <summary>
///     Manages action registration and execution for palette items
/// </summary>
public class ActionBinding<TItem> where TItem : BaseObservableListItem, IPaletteListItem {
    private readonly List<PaletteAction<TItem>> _actions = new();

    /// <summary>
    ///     Registers an action with the binding system
    /// </summary>
    public void Register(PaletteAction<TItem> action) => this._actions.Add(action);

    /// <summary>
    ///     Registers multiple actions
    /// </summary>
    public void RegisterRange(IEnumerable<PaletteAction<TItem>> actions) => this._actions.AddRange(actions);

    /// <summary>
    ///     Finds and executes the matching action for a keyboard event
    /// </summary>
    public async Task<bool> TryExecuteAsync(TItem item, Key key, ModifierKeys modifiers) {
        try {
            var action = this.FindMatchingAction(key, modifiers);
            if (action == null || !action.CanExecute(item)) return false;

            await this.ExecuteActionInternalAsync(action, item);
            return true;
        } catch (Exception ex) {
            Debug.WriteLine($"Error executing action: {ex.Message} : \n{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    ///     Finds and executes the matching action for a mouse event
    /// </summary>
    public async Task<bool> TryExecuteAsync(TItem item, ModifierKeys modifiers) {
        try {
            var action = this.FindMatchingAction(null, modifiers);
            if (action == null || !action.CanExecute(item)) return false;

            await this.ExecuteActionInternalAsync(action, item);
            return true;
        } catch (Exception ex) {
            Debug.WriteLine($"Error executing action: {ex.Message} : \n{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    ///     Gets all available actions for a given item (filtered by CanExecute)
    /// </summary>
    public IEnumerable<PaletteAction<TItem>> GetAvailableActions(TItem item) =>
        this._actions.Where(a => a.CanExecute(item));

    /// <summary>
    ///     Gets all registered actions (not filtered by CanExecute)
    /// </summary>
    public IEnumerable<PaletteAction<TItem>> GetAllActions() => this._actions;

    /// <summary>
    ///     Executes a specific action for a given item
    /// </summary>
    public async Task ExecuteActionAsync(PaletteAction<TItem> action, TItem item) {
        if (!action.CanExecute(item))
            throw new InvalidOperationException($"Action '{action.Name}' cannot execute for this item");

        await this.ExecuteActionInternalAsync(action, item);
    }

    /// <summary>
    ///     Internal helper that executes either synchronous or asynchronous action
    /// </summary>
    private async Task ExecuteActionInternalAsync(PaletteAction<TItem> action, TItem item) {
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

    private PaletteAction<TItem> FindMatchingAction(Key? key, ModifierKeys modifiers) {

        // Find exact matches first (most specific)
        var exactMatch = this._actions.FirstOrDefault(a =>
            a.Modifiers == modifiers &&
            ((key.HasValue && a.Key == key) || (!key.HasValue && a.Key == null)));

        if (exactMatch != null) return exactMatch;

        // Fall back to default action (no modifiers, no specific key/button)
        return this._actions.FirstOrDefault(a =>
            a.Modifiers == ModifierKeys.None &&
            a.Key == null);
    }
}