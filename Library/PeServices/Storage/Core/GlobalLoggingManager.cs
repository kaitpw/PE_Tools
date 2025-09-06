namespace PeServices.Storage.Core;

public class GlobalLoggingManager {
    private const string _dateTimeFormat = "yyyy-MM-dd HH:mm:ss";
    private const int _maxLines = 500;
    private readonly string _logFilePath;
    private readonly string _basePath;

    public GlobalLoggingManager(string basePath) {
        this._basePath = basePath;
        this._logFilePath = Path.Combine(this._basePath, "log.txt");
        _ = Directory.CreateDirectory(this._basePath);
    }

    public void Write(string message) {
        this.CleanLog();
        var logEntry = $"({DateTime.Now.ToString(_dateTimeFormat)}) {message}{Environment.NewLine}";
        File.AppendAllText(this._logFilePath, logEntry);
    }

    private void CleanLog() {
        if (!File.Exists(this._logFilePath)) return;
        var lines = File.ReadAllLines(this._logFilePath);
        if (lines.Length <= _maxLines) return;
        var recentLines = lines.Skip(lines.Length - _maxLines).ToArray();
        File.WriteAllLines(this._logFilePath, recentLines);
    }
}