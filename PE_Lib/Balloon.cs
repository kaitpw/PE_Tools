using System.Text;

namespace PE_Lib;

/// <summary>Message collector for accumulating messages, then showing all at once</summary>
internal class Balloon(string title = null) {
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


    /// <summary>Clear all accumulated messages</summary>
    public void Clear() => this._messages.Clear();

    /// <summary>Show accumulated messages in a copyable balloon</summary>
    public void Show() {
        var combinedMessage = new StringBuilder();
        combinedMessage.AppendLine(new string('-', 20));
        if (this._messages.Count == 0) this.Add(LogLevel.WARN, "No messages to display");

        foreach (var message in this._messages)
            combinedMessage.AppendLine("\u2588 " + message);

        UiUtils.ShowBalloon(combinedMessage.ToString(), title);
        this.Clear();
    }
}