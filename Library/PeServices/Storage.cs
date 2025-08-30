using PeUtils;

namespace PeServices;

/// <summary>
///     Base settings for the settings subservice service. Merge these with T for the settings manager. when the time comes
/// </summary>
public class Settings {
    public bool EnableCrossSessionState { get; set; } = true;
    public bool OpenOutputFilesOnCommandFinish { get; set; } = false;
    public bool EnableLastRunLoggingToOutput { get; set; } = false;
}

public class Storage {
    private readonly string _basePath;
    // public SettingsManager<T> Settings<T>() where T : class => new SettingsManager<T>(this, _basePath);
    // public TempManager Temp() => new TempManager(this, _basePath);
    // public OutputManager Output() => new OutputManager(this, _basePath);

    public Storage(string addinName) =>
        this._basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Assembly.GetExecutingAssembly().GetName().Name ?? "PE_Tools",
            addinName);

    /// <summary>
    ///     State Manager for the state/ storage dir. Handles csv and json files
    /// </summary>
    /// <typeparam name="T">A record type representing a csv row. Or a JSON schema</typeparam>
    public StateManager<T> State<T>() where T : class, new() => new(this._basePath);
}

public class StateManager<T> where T : class, new() {
    private readonly string _stateSubPath;
    public string _fullPathCsv;
    public string _fullPathJson;

    public StateManager(string basePath) {
        _ = Directory.CreateDirectory(Path.Combine(basePath, "state"));
        this._stateSubPath = Path.Combine(basePath, "state");
        this._fullPathCsv = Path.Combine(this._stateSubPath, "state.csv");
        this._fullPathJson = Path.Combine(this._stateSubPath, "state.json");
    }

    public StateManager<T> NonDefaultFile(string filename) {
        switch (filename) {
        case string s when s.EndsWith(".csv"): // 'when' clause for condition
            this._fullPathCsv = Path.Combine(this._stateSubPath, filename);
            break;
        case string s when s.EndsWith(".json"):
            this._fullPathJson = Path.Combine(this._stateSubPath, filename);
            break;
        default:
            throw new ArgumentException($"Invalid file type for filename: '{filename}'. Expected '.csv' or '.json'.");
        }

        return this;
    }

    public Dictionary<string, T> ReadCsv() => Csv.ReadCsv<T>(this._fullPathCsv);
    public T ReadCsvRow(string key) => Csv.ReadCsvRow<T>(this._fullPathCsv, key);
    public void WriteCsv(Dictionary<string, T> data) => Csv.WriteCsv(this._fullPathCsv, data);
    public void WriteCsvRow(string key, T data) => Csv.WriteCsvRow(this._fullPathCsv, key, data);
}