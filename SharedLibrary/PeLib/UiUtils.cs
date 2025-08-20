using Autodesk.Internal.InfoCenter;
using Autodesk.Windows;
using System.Windows;

namespace PeLib;

internal class UiUtils {
    /// <summary>Shows a balloon tip with custom click</summary>
    /// <param name="text">Text to display</param>
    /// <param name="title">Optional category for the balloon</param>
    /// <param name="clickHandler">Custom action to perform on click, if empty, default is copy text</param>
    /// <param name="clickDescription">Optional tooltip text shown on hover</param>
    public static void ShowBalloon(
        Action clickHandler,
        string clickDescription,
        string text,
        string title = null
    ) {
        if (text == null)
            return;
        if (title == null)
            title = typeof(Utils).Assembly.GetName().Name;

        var ri = new ResultItem {
            Title = text.Trim(), Category = title + (clickDescription != "" ? " (" + clickDescription + ")" : null)
        };
        ri.ResultClicked += (_, _) => clickHandler();

        ComponentManager.InfoCenterPaletteManager.ShowBalloon(ri);
    }

    /// <summary>Shows a balloon tip with a click-to-copy handler</summary>
    /// <param name="text">Text to display</param>
    /// <param name="title">Optional category for the balloon</param>
    public static void ShowBalloon(
        string text,
        string title = null
    ) {
        if (text == null)
            return;

        title ??= typeof(Utils).Assembly.GetName().Name;
        var ri = new ResultItem { Title = text.Trim(), Category = title + " (Click to copy)" };
        ri.ResultClicked += (_, _) => Clipboard.SetText(text.Trim());

        ComponentManager.InfoCenterPaletteManager.ShowBalloon(ri);
    }

    /// <summary>Shows a balloon tip only visible in a DEBUG builds</summary>
    /// <param name="text">Text to display</param>
    /// <param name="title">Optional category for the balloon</param>
    public static void ShowDebugBalloon(
        string text,
        string title = null
    ) {
#if DEBUG
        ShowBalloon(text, title);
#endif
    }
}