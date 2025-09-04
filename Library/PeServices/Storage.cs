using Json.Schema.Generation;
using PeUtils.Files;

namespace PeServices;

public class Storage(string addinName) {
    private readonly string _basePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Assembly.GetExecutingAssembly().GetName().Name ?? "PE_Tools",
        addinName);

    /// <summary>
    ///     Manager for the `settings\` storage dir. Handles granular read-only to JSON and CSV.
    /// </summary>
    /// <typeparam name="T">Type of data stored. A JSON schema.</typeparam>
    /// <remarks>
    ///     Use this manager to store add-in settings that should be persisted between Revit sessions.
    ///     Data here should be updated but never completely overwritten (unless via an import).
    ///     The default file path is `{addinName}/settings/settings.json`
    /// </remarks>
    public SettingsManager Settings() => new(this._basePath);

    /// <summary>
    ///     Manager for the `state\` storage dir. Handles granular read/write to CSV and JSON.
    /// </summary>
    /// <typeparam name="T">Type of stored data. Either a JSON schema or a record representing a csv row.</typeparam>
    /// <remarks>
    ///     Use this manager to store add-in state that should be persisted between Revit sessions.
    ///     Data here is meant to be frequently granularly updated but never overwritten (unless via an import).
    ///     The default file path is `{addinName}/state/state.json`
    /// </remarks>
    public StateManager State() => new(this._basePath);

    /// TODO: figure out how to support any file type while still keeping csv and json type-safety.
    /// <summary>
    ///     Manager for the `output\` storage dir. Handles full (non-granular) writes to any file type.
    /// </summary>
    /// <typeparam name="T">A record type representing a csv row. Or a JSON schema or PDF or RVT or any other file type</typeparam>
    /// <remarks>
    ///     Use this manager to save add-in output for the user.
    ///     Data here should be written once to and then opened by the user.
    ///     There is NO DEFAULT FILE PATH for output files.
    /// </remarks>
    public OutputManager Output() => new(this._basePath);

    // /// TODO: figure out how to support any file type while still keeping csv and json type-safety.
    // /// <summary>
    // ///     Manager for the `temp\` storage dir. Handles full (non-granular) writes to any file type.
    // /// </summary>
    // /// <typeparam name="T">A record type representing a csv row. Or a JSON schema or PDF or RVT or any other file type</typeparam>
    // /// <remarks>
    // ///     Use this manager to store temporary add-in data needed while it's running or between runs.
    // ///     The use for between-run storage is important for error-prone batch processes 
    // ///     where failures need to be recorded for later rectification/review.
    // ///     Thus, data here should be treated as *mostly* ephemeral and should be deleted on addin *success*.
    // ///     Data here should/can be overwritten or granularly read/written.
    // ///     There is NO DEFAULT FILE PATH for temp files.
    // /// </remarks>
    // public TempManager<T> Temp<T>() where T : class, new() => new(this._basePath);
}

public class SettingsManager {
    private readonly string _thisPath;

    public SettingsManager(string basePath) {
        this._thisPath = Path.Combine(basePath, "settings");
        _ = Directory.CreateDirectory(this._thisPath);
    }

    public JsonReader<T> Json<T>() where T : class, new() =>
        new(new Json<T>(Path.Combine(this._thisPath, "settings.json")));

    public JsonReader<T> Json<T>(string filename) where T : class, new() =>
        new(new Json<T>(Path.Combine(this._thisPath, filename)));

    /// <summary> Base interface for all settings classes. Provides global settings properties.</summary>
    public abstract class BaseSettings {
        [Description(
            "Current profile to use for the command. This determines which profile is used in the next launch of a command.")]
        public string CurrentProfile { get; set; } = "";

        [Description(
            "Profiles for the command. The profile that a command uses is determined by the `CurrentProfile` property.")]
        public List<object> Profiles { get; set; } = [];
    }
}

public class StateManager {
    private readonly string _thisPath;

    public StateManager(string basePath) {
        this._thisPath = Path.Combine(basePath, "state");
        _ = Directory.CreateDirectory(this._thisPath);
    }

    public JsonReadWriter<T> Json<T>() where T : class, new() =>
        new(new Json<T>(Path.Combine(this._thisPath, "state.json")));

    public JsonReadWriter<T> Json<T>(string filename) where T : class, new() =>
        new(new Json<T>(Path.Combine(this._thisPath, filename)));

    public CsvReadWriter<T> Csv<T>() where T : class, new() =>
        new(new Csv<T>(Path.Combine(this._thisPath, "state.csv")));

    public CsvReadWriter<T> Csv<T>(string filename) where T : class, new() =>
        new(new Csv<T>(Path.Combine(this._thisPath, filename)));
}

public class OutputManager {
    private readonly string _thisPath;

    public OutputManager(string basePath) {
        this._thisPath = Path.Combine(basePath, "output");
        _ = Directory.CreateDirectory(this._thisPath);
    }

    public JsonWriter<T> Json<T>() where T : class, new() =>
        new(new Json<T>(Path.Combine(this._thisPath, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json")));

    public JsonWriter<T> Json<T>(string filename) where T : class, new() =>
        new(new Json<T>(Path.Combine(this._thisPath, filename)));

    public CsvWriter<T> Csv<T>() where T : class, new() =>
        new(new Csv<T>(Path.Combine(this._thisPath, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv")));

    public CsvWriter<T> Csv<T>(string filename) where T : class, new() =>
        new(new Csv<T>(Path.Combine(this._thisPath, filename)));
}

//////////////////////////////////////////////////////////////////////////////////
// Restricted interfaces for different operation types

public class JsonReader<T>(Json<T> json) where T : class, new() {
    public string FilePath => json.FilePath;
    public T Read() => json.Read();
}

public class JsonWriter<T>(Json<T> json) where T : class, new() {
    public string FilePath => json.FilePath;
    public void Write(T data) => json.Write(data);
}

public class JsonReadWriter<T>(Json<T> json) where T : class, new() {
    public string FilePath => json.FilePath;
    public T Read() => json.Read();
    public void Write(T data) => json.Write(data);
}

public class CsvReader<T>(Csv<T> csv) where T : class, new() {
    public string FilePath => csv.FilePath;
    public Dictionary<string, T> Read() => csv.Read();
}

public class CsvWriter<T>(Csv<T> csv) where T : class, new() {
    public string FilePath => csv.FilePath;
    public void Write(Dictionary<string, T> data) => csv.Write(data);
}

public class CsvReadWriter<T>(Csv<T> csv) where T : class, new() {
    public string FilePath => csv.FilePath;
    public Dictionary<string, T> Read() => csv.Read();
    public void Write(Dictionary<string, T> data) => csv.Write(data);
    public T? ReadRow(string key) => csv.ReadRow(key);
    public void WriteRow(string key, T rowData) => csv.WriteRow(key, rowData);
}