using PeUtils.Files;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PeServices.Storage.Core;

public class GlobalStateManager {
    private readonly string _basePath;
    private readonly string _stateFilePath;

    public GlobalStateManager(string basePath, string filename) {
        this._basePath = basePath;
        this._stateFilePath = Path.Combine(this._basePath, filename);
        _ = Directory.CreateDirectory(this._basePath);
    }

    public JsonReadWriter<T> Json<T>() where T : class, new() =>
        new(new Json<T>(this._stateFilePath, false));


    public CsvReadWriter<T> Csv<T>() where T : class, new() =>
        new(new Csv<T>(this._stateFilePath));
}

public class GlobalLoggingManager {
    private const string _dateTimeFormat = "yyyy-MM-dd HH:mm:ss";
    private const int _maxLines = 500;
    private readonly string _basePath;
    private readonly string _logFilePath;

    public GlobalLoggingManager(string basePath) {
        this._basePath = basePath;
        this._logFilePath = Path.Combine(this._basePath, "log.txt");
        _ = Directory.CreateDirectory(this._basePath);
    }

    public void Write(string message) {
        this.CleanLog();
        var logEntry =
            $"({DateTime.Now.ToString(_dateTimeFormat)}) {message}{Environment.NewLine}{Environment.NewLine}";
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

public class GlobalSettingsManager {
    private readonly string _basePath;
    private readonly string _settingsFilePath;


    public GlobalSettingsManager(string basePath) {
        this._basePath = basePath;
        this._settingsFilePath = Path.Combine(this._basePath, "settings.json");
        _ = Directory.CreateDirectory(this._basePath);
    }

    public JsonReader<GlobalSettings> Json() =>
        new(new Json<GlobalSettings>(this._settingsFilePath, true));


    /// <summary> Base interface for all settings classes. Provides global settings properties.</summary>
    public class GlobalSettings {
        [Description(
            "The desktop-app client id of the Autodesk Platform Services app. If none exists yet, make a 'Desktop App' at https://aps.autodesk.com/hubs/@personal/applications/")]
        [Required]
        public string ApsDesktopClientId1 { get; set; } = "";

        [Description(
            "The web-app client id of the Autodesk Platform Services app. If none exists yet, make a 'Traditional Web App' at https://aps.autodesk.com/hubs/@personal/applications/")]
        [Required]
        public string ApsWebClientId1 { get; set; } = "";

        [Description(
            "The web-app client secret of the Autodesk Platform Services app. If none exists yet, make a 'Traditional Web App' at https://aps.autodesk.com/hubs/@personal/applications/")]
        [Required]
        public string ApsWebClientSecret1 { get; set; } = "";

        [Description(
            "The account ID derived from an 'id' field returned by `project/v1/hubs` but with the 'b.' prefix sliced off. If left empty, the first item of 'data' will be used.")]
        [Required]
        public string Bim360AccountId { get; set; } = "";

        [Description(
            "The group ID derived from an 'id' field returned by `parameters/v1/accounts/<accountId>/groups`. If left empty, the first item of 'results' will be used.")]
        [Required]
        public string ParamServiceGroupId { get; set; } = "";

        [Description(
            "The collection ID derived from an 'id' field returned by `parameters/v1/accounts/<accountId>/groups/<groupId>/collections`. If left empty, the first item of 'results' will be used.")]
        public string ParamServiceCollectionId { get; set; } = "";
    }
}