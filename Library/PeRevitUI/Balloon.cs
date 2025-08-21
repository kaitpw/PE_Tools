using Autodesk.Internal.InfoCenter;
using Autodesk.Windows;
using System.Text;
using System.Windows;
using PeRevitUtils;

namespace PeRevitUI;

/// <summary>Message collector for accumulating messages, then showing all at once</summary>
internal class Balloon() {
    public enum LogLevel {
        // ReSharper disable once InconsistentNaming
        INFO,

        // ReSharper disable once InconsistentNaming
        WARN,

        // ReSharper disable once InconsistentNaming
        ERR
    }

    private const string FmtNormal = "{0}: {1}";
    private const string FmtMethod = "{0} ({1}): {2}";
    private const string FmtErrorTrace = "{0} ({1}): {2}\n{3}";
    private const string StrNoMethod = "No Method Found";
    private readonly List<string> _messages = [];

    /// <summary>Clear all accumulated messages</summary>
    public void Clear() => this._messages.Clear();

    /// <summary>Add a normal message (with a Log Level)</summary>
    public void Add(LogLevel logLevel, string message) {
        if (!string.IsNullOrWhiteSpace(message))
            this._messages.Add(string.Format(FmtNormal, logLevel, message.Trim()));
    }

    /// <summary>Add a normal message (with the method's name)</summary>
    public void Add(LogLevel logLevel, StackFrame sf, string message) {
        var method = sf.GetMethod()?.Name ?? StrNoMethod;
        if (!string.IsNullOrWhiteSpace(message))
            this._messages.Add(string.Format(FmtMethod, logLevel, method, message.Trim()));
    }

    /// <summary>Add an error message (with an optional stack trace)</summary>
    public void Add(StackFrame sf, Exception ex, bool trace = false) {
        var method = sf.GetMethod()?.Name ?? StrNoMethod;
        this._messages.Add(trace
            ? string.Format(FmtErrorTrace, LogLevel.ERR, method, ex.Message, ex.StackTrace)
            : string.Format(FmtMethod, LogLevel.ERR, method, ex.Message));
    }

    /// <summary>Add a DEBUG build message</summary>
    public void AddDebug(LogLevel logLevel, StackFrame sf, string message) {
#if DEBUG
        var method = sf.GetMethod()?.Name ?? StrNoMethod;
        var prefix = "DEBUG " + logLevel;
        if (!string.IsNullOrWhiteSpace(message))
            this._messages.Add(string.Format(FmtMethod, prefix, method, message.Trim()));
#endif
    }

    /// <summary>Add a DEBUG build error message (with an optional stack trace)</summary>
    public void AddDebug(StackFrame sf, Exception ex, bool trace = false) {
#if DEBUG
        var method = sf.GetMethod()?.Name ?? StrNoMethod;
        var prefix = "DEBUG " + LogLevel.ERR;
        this._messages.Add(trace
            ? string.Format(FmtErrorTrace, prefix, method, ex.Message, ex.StackTrace)
            : string.Format(FmtMethod, prefix, method, ex.Message));
#endif
    }

    /// <summary>Show multi-message balloon with a click-to-copy handler</summary>
    /// <param name="title">Optional title for the balloon</param>
    public void ShowMulti(
        string title = null
    ) {
        var combinedMessage = new StringBuilder();
        _ = combinedMessage.AppendLine(new string('-', 20));
        if (this._messages.Count == 0) this.Add(LogLevel.WARN, "No messages to display");

        foreach (var message in this._messages)
            _ = combinedMessage.AppendLine("\u2588 " + message);

        ShowSingle(combinedMessage.ToString(), title);
        this.Clear();
    }

    /// <summary>Show multi-message balloon with a custom click handler</summary>
    /// <param name="clickHandler">Custom action to perform on click</param>
    /// <param name="clickDescription">Click action description. (i.e. "Click to ...")</param>
    /// <param name="title">Optional title for the balloon</param>
    public void ShowMulti(
        Action clickHandler,
        string clickDescription,
        string title = null
    ) {
        var combinedMessage = new StringBuilder();
        _ = combinedMessage.AppendLine(new string('-', 20));
        if (this._messages.Count == 0) this.Add(LogLevel.WARN, "No messages to display");

        foreach (var message in this._messages)
            _ = combinedMessage.AppendLine("\u2588 " + message);

        ShowSingle(clickHandler, clickDescription, combinedMessage.ToString(), title);
        this.Clear();
    }

    /// <summary>Show single-message balloon with a click-to-copy handler</summary>
    /// <param name="text">Text to display</param>
    /// <param name="title">Optional title for the balloon</param>
    public static void ShowSingle(
        string text,
        string title = null
    ) {
        if (text == null)
            return;

        title ??= typeof(Utils).Assembly.GetName().Name;
#pragma warning disable CA1416 // Validate platform compatibility
        var ri = new ResultItem { Title = text.Trim(), Category = title + " (Click to copy)" };
        ri.ResultClicked += (_, _) => Clipboard.SetText(text.Trim());
        ComponentManager.InfoCenterPaletteManager.ShowBalloon(ri);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    /// <summary>Show single-message balloon with a custom click handler</summary>
    /// <param name="clickHandler">Custom action to perform on click</param>
    /// <param name="clickDescription">Click action description. (i.e. "Click to ...")</param>
    /// <param name="text">Text to display</param>
    /// <param name="title">Optional title for the balloon</param>
    public static void ShowSingle(
        Action clickHandler,
        string clickDescription,
        string text,
        string title = null
    ) {
        if (text == null)
            return;
        if (title == null)
            title = typeof(Utils).Assembly.GetName().Name;
#pragma warning disable CA1416 // Validate platform compatibility
        var ri = new ResultItem {
            Title = text.Trim(),
            Category = title + (clickDescription != "" ? " (" + clickDescription + ")" : null)
        };
        ri.ResultClicked += (_, _) => clickHandler();

        ComponentManager.InfoCenterPaletteManager.ShowBalloon(ri);
#pragma warning restore CA1416 // Validate platform compatibility

    }


    /// <summary>Show single-message balloon with a click-to-copy handler. Only visible in a DEBUG builds</summary>
    /// <param name="text">Text to display</param>
    /// <param name="title">Optional title for the balloon</param>
    public static void ShowSingleDebug(
        string text,
        string title = null
    ) =>
#if DEBUG
        ShowSingle(text, title);
#endif

    /// <summary>Show single-message balloon with a click-to-copy handler. Only visible in a DEBUG builds</summary>
    /// <param name="clickHandler">Custom action to perform on click</param>
    /// <param name="clickDescription">Click action description. (i.e. "Click to ...")</param>
    /// <param name="text">Text to display</param>
    /// <param name="title">Optional title for the balloon</param>
    public static void ShowSingleDebug(
        Action clickHandler,
        string clickDescription,
        string text,
        string title = null
    ) =>
#if DEBUG
        ShowSingle(clickHandler, clickDescription, text, title);
#endif

}


