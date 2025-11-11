using AddinCmdPalette.Core;
using System.Windows.Input;

namespace AddinCmdPalette.Actions;

/// <summary>
///     Represents a single action that can be triggered in the palette
/// </summary>
public record PaletteAction {
    /// <summary> Display name for the action (for debugging/logging) </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary> Keyboard modifiers required (Ctrl, Shift, Alt, etc.) </summary>
    public ModifierKeys Modifiers { get; init; } = ModifierKeys.None;

    /// <summary> Mouse button that triggers this action (null for keyboard-only) </summary>
    public MouseButton? MouseButton { get; init; }

    /// <summary> Keyboard key that triggers this action (null for mouse-only) </summary>
    public Key? Key { get; init; }

    /// <summary> Async execution function </summary>
    public Func<ISelectableItem, Task> ExecuteAsync { get; init; } = null!;

    /// <summary> Optional predicate to check if action can execute </summary>
    public Func<ISelectableItem, bool> CanExecute { get; init; } = _ => true;
}