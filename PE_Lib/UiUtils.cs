using Autodesk.Internal.InfoCenter;
using Autodesk.Windows;
using System.Text;
using System.Windows;

namespace PE_Lib;

internal class UiUtils {
    /// <summary>Shows a balloon tip with custom click</summary>
    /// <param name="text">Text to display</param>
    /// <param name="title">Optional category for the balloon</param>
    /// <param name="clickHandler">Custom action to perform on click, if empty, default is copy text</param>
    /// <param name="clickDescription">Optional tooltip text shown on hover</param>
    public static void ShowBalloon(
        string text,
        string title = null,
        Action clickHandler = null,
        string clickDescription = null
    ) {
        var clickHandlerDefault = () => Clipboard.SetText(text.Trim());

        if (text == null)
            return;
        if (title == null)
            title = typeof(Utils).Assembly.GetName().Name;
        if (clickHandler == null)
            clickHandler = clickHandlerDefault;
        if (clickHandler.Method.Equals(clickHandlerDefault))
            clickDescription = "Click to copy";

        var ri = new ResultItem();
        ri.Title = text.Trim();
        ri.Category = title + (clickDescription != "" ? " (" + clickDescription + ")" : null);
        ri.ResultClicked += (_, _) => clickHandler();

        ComponentManager.InfoCenterPaletteManager.ShowBalloon(ri);
    }
}

/// <summary>Message collector for accumulating messages, then showing all at once</summary>
internal class BalloonCollector {
    private readonly List<string> _messages;
    private readonly string _title;

    public BalloonCollector(string title = null) {
        this._messages = new List<string>();
        this._title = title;
    }

    /// <summary>Add a message to the message list</summary>
    public void AddMessage(string message) {
        if (!string.IsNullOrWhiteSpace(message)) this._messages.Add($"{message.Trim()}");
    }

    /// <summary>Add a formatted message with prefix </summary>
    public void AddDebugMessage(string description, string message) {
#if DEBUG
        if (!string.IsNullOrWhiteSpace(message)) this._messages.Add($"DEBUG ({description}): {message.Trim()}");
#endif
    }

    /// <summary>Add an exception message</summary>
    public void AddException(string context, Exception ex) => this._messages.Add($"ERROR ({context}): {ex.Message}");

    /// <summary>Clear all accumulated messages</summary>
    public void Clear() => this._messages.Clear();

    /// <summary>Show accumulated messages in a copyable balloon</summary>
    public void Show() {
        var combinedMessage = new StringBuilder();
        combinedMessage.AppendLine(new string('-', 20));
        if (this._messages.Count == 0) this.AddMessage("No messages to display");

        foreach (var message in this._messages)
            combinedMessage.AppendLine("\u2588 " + message);

        UiUtils.ShowBalloon(combinedMessage.ToString(), this._title);
        this.Clear();
    }
}