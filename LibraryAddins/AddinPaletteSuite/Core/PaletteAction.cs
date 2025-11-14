using System.Windows.Input;

namespace AddinPaletteSuite.Core;

/// <summary>
///     Represents a single action that can be triggered in the palette
/// </summary>
public record PaletteAction<TItem> where TItem : BaseObservableListItem, IPaletteListItem {
    /// <summary> Display name for the action (for debugging/logging) </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary> Keyboard modifiers required (Ctrl, Shift, Alt, etc.) </summary>
    public ModifierKeys Modifiers { get; init; } = ModifierKeys.None;

    /// <summary> Keyboard key that triggers this action </summary>
    public Key? Key { get; init; }

    /// <summary> Synchronous execution function </summary>
    public Action<TItem> Execute { get; init; }

    /// <summary> Async execution function </summary>
    public Func<TItem, Task> ExecuteAsync { get; init; }

    /// <summary> Synchronous execution function that opens the next palette (closes current without restoring focus) </summary>
    public Action<TItem> ExecuteNextPalette { get; init; }

    /// <summary> Async execution function that opens the next palette (closes current without restoring focus) </summary>
    public Func<TItem, Task> ExecuteNextPaletteAsync { get; init; }

    /// <summary> Optional predicate to check if action can execute </summary>
    public Func<TItem, bool> CanExecute { get; init; } = _ => true;
}