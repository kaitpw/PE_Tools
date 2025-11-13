using System.Windows.Input;

namespace AddinPaletteSuite.Core;

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

    /// <summary> Synchronous execution function </summary>
    public Action<IPaletteListItem>? Execute { get; init; }

    /// <summary> Async execution function </summary>
    public Func<IPaletteListItem, Task>? ExecuteAsync { get; init; }

    /// <summary> Optional predicate to check if action can execute </summary>
    public Func<IPaletteListItem, bool> CanExecute { get; init; } = _ => true;
}