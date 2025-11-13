using System.Windows;

namespace AddinCmdPalette.Core;

/// <summary>
///     Interface for popovers that can exit and return focus to their parent
/// </summary>
public interface IPopoverExit {
    /// <summary>
    ///     Event raised when the popover requests to exit
    /// </summary>
    event EventHandler ExitRequested;

    /// <summary>
    ///     Sets the target element to return focus to when exiting
    /// </summary>
    UIElement? ReturnFocusTarget { get; set; }

    /// <summary>
    ///     Requests the popover to exit and return focus
    /// </summary>
    void RequestExit();
}

