using PeUtils.Files;

namespace PeServices;

public class Storage(string addinName) {
    private readonly string _basePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Assembly.GetExecutingAssembly().GetName().Name ?? "PE_Tools",
        addinName);

    /// <summary>
    ///     Manager for the `settings\` storage dir. Handles granular read/writes to JSON only.
    /// </summary>
    /// <typeparam name="T">Type of data stored. A JSON schema.</typeparam>
    /// <remarks>
    ///     Use this manager to store add-in settings that should be persisted between Revit sessions.
    ///     Data here should be updated but never completely overwritten (unless via an import).
    ///     The default file path is `{addinName}/settings/settings.json`
    /// </remarks>
    public SettingsManager<T> Settings<T>() where T : class, SettingsManager<T>.IBaseSettings, new() =>
        new(this._basePath);

    /// <summary>
    ///     Manager for the `state\` storage dir. Handles granular read/write to CSV and JSON.
    /// </summary>
    /// <typeparam name="T">Type of stored data. Either a JSON schema or a record representing a csv row.</typeparam>
    /// <remarks>
    ///     Use this manager to store add-in state that should be persisted between Revit sessions.
    ///     Data here is meant to be frequently granularly updated but never overwritten (unless via an import).
    ///     The default file path is `{addinName}/state/state.json`
    /// </remarks>
    public StateManager<T> State<T>() where T : class, new() => new(this._basePath);

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
    public OutputManager<T> Output<T>() where T : class, new() => new(this._basePath);

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

public class SettingsManager<T> where T : class, SettingsManager<T>.IBaseSettings, new() {
    private readonly string _thisPath;

    public SettingsManager(string basePath) {
        this._thisPath = Path.Combine(basePath, "settings");
        _ = Directory.CreateDirectory(this._thisPath);
    }

    public Json<T> Json() => new(Path.Combine(this._thisPath, "settings.json"));

    /// <summary> Base interface for all settings classes. Provides global settings properties.</summary>
    public interface IBaseSettings {
        bool OpenOutputFilesOnCommandFinish { get; set; }
    }
}

public class StateManager<T> where T : class, new() {
    private readonly string _thisPath;

    public StateManager(string basePath) {
        this._thisPath = Path.Combine(basePath, "state");
        _ = Directory.CreateDirectory(this._thisPath);
    }

    public Json<T> Json() => new(Path.Combine(this._thisPath, "state.json"));
    public Csv<T> Csv() => new(Path.Combine(this._thisPath, "state.csv"));
    public Csv<T> Csv(string filename) => new(Path.Combine(this._thisPath, filename));
}

public class OutputManager<T> where T : class, new() {
    private readonly string _thisPath;

    public OutputManager(string basePath) {
        this._thisPath = Path.Combine(basePath, "output");
        _ = Directory.CreateDirectory(this._thisPath);
    }

    public Csv<T> Csv() => new(Path.Combine(this._thisPath, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"));
    public Csv<T> Csv(string filename) => new(Path.Combine(this._thisPath, filename));
}