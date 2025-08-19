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
        clickDescription = "Click to copy";

        var ri = new ResultItem();
        ri.Title = text.Trim();
        ri.Category = title + (clickDescription != "" ? " (" + clickDescription + ")" : null);
        ri.ResultClicked += (_, _) => clickHandler();

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

/// <summary>Message collector for accumulating messages, then showing all at once</summary>
internal class BalloonCollector {
    private readonly List<string> _messages;
    private readonly string _title;

    private static readonly string FMT_NORMAL = "{0}: {1}";
    private static readonly string FMT_METHOD = "{0} ({1}): {2}";
    private static readonly string FMT_ERROR_TRACE = "{0} ({1}): {2}\n{3}";
    private static readonly string STR_NO_METHOD = "No Method Found";

    public enum LogLevel {
        INFO,
        WARNING,
        ERROR,
    }

    public BalloonCollector(string title = null) {
        this._messages = new List<string>();
        this._title = title;
    }

    /// <summary>Add a normal message (with a Log Level)</summary>
    public void Add(LogLevel logLevel, string message) {
        if (!string.IsNullOrWhiteSpace(message))
            this._messages.Add(string.Format(FMT_NORMAL, logLevel, message.Trim()));
    }

    /// <summary>Add a normal message (with the method's name)</summary>
    public void Add(LogLevel logLevel, StackFrame sf, string message) {
        var method = sf.GetMethod().Name ?? STR_NO_METHOD;
        if (!string.IsNullOrWhiteSpace(message))
            this._messages.Add(string.Format(FMT_METHOD, logLevel, method, message.Trim()));
    }

    /// <summary>Add an error message (with an optional stack trace)</summary>
    public void Add(StackFrame sf, Exception ex, bool trace = false) {
        var method = sf.GetMethod().Name ?? STR_NO_METHOD;
        if (trace)
            this._messages.Add(string.Format(FMT_ERROR_TRACE, LogLevel.ERROR, method, ex.Message, ex.StackTrace));
        else
            this._messages.Add(string.Format(FMT_METHOD, LogLevel.ERROR, method, ex.Message));
    }

    /// <summary>Add a DEBUG build message</summary>
    public void AddDebug(LogLevel logLevel, StackFrame sf, string message) {
#if DEBUG
        var method = sf.GetMethod().Name ?? STR_NO_METHOD;
        var prefix = "DEBUG " + logLevel;
        if (!string.IsNullOrWhiteSpace(message))
            this._messages.Add(string.Format(FMT_METHOD, prefix, method, message.Trim()));
#endif
    }

    /// <summary>Add a DEBUG build error message (with an optional stack trace)</summary>
    public void AddDebug(StackFrame sf, Exception ex, bool trace = false) {
#if DEBUG
        var method = sf.GetMethod().Name ?? STR_NO_METHOD;
        var prefix = "DEBUG " + LogLevel.ERROR;
        if (trace)
            this._messages.Add(string.Format(FMT_ERROR_TRACE, prefix, method, ex.Message, ex.StackTrace));
        else
            this._messages.Add(string.Format(FMT_METHOD, prefix, method, ex.Message));
#endif
    }


    /// <summary>Clear all accumulated messages</summary>
    public void Clear() => this._messages.Clear();

    /// <summary>Show accumulated messages in a copyable balloon</summary>
    public void Show() {
        var combinedMessage = new StringBuilder();
        combinedMessage.AppendLine(new string('-', 20));
        if (this._messages.Count == 0) this.Add(LogLevel.WARNING, "No messages to display");

        foreach (var message in this._messages)
            combinedMessage.AppendLine("\u2588 " + message);

        UiUtils.ShowBalloon(combinedMessage.ToString(), this._title);
        this.Clear();
    }
}