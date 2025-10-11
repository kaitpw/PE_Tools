using PeServices.Storage.Core;

namespace PeServices.Storage;

public class Storage(string addinName) {
    private static readonly string BasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Assembly.GetExecutingAssembly().GetName().Name ?? "PE_Tools");

    private readonly string _addinPath = Path.Combine(BasePath, addinName);

    /// <summary>
    ///     Manager for the settings.json in the base storage dir. Handles only reads to the file.
    /// </summary>
    /// <remarks>
    ///     Use this manager to store global add-in settings that should be persisted between Revit sessions.
    ///     There is no non-default file path, it is always `{basePath}/settings.json`
    /// </remarks>
    public static GlobalSettingsManager GlobalSettings() => new(BasePath);

    /// <summary>
    ///     Manager for global state json or csv files in the base storage dir.
    ///     Handles granular read/write to CSV, and full (non-granular) read/write to JSON.
    /// </summary>
    /// <remarks>
    ///     Use this manager to store global add-in state that should be persisted between Revit sessions.
    ///     There is no non-default file path, it is always `{basePath}/{filename}.json` or `{basePath}/{filename}.csv`
    /// </remarks>
    public static GlobalStateManager GlobalState(string filename) => new(BasePath, filename);

    /// <summary>
    ///     Manager for the log.txt in the base storage dir. Handles only writes to the file with auto cleanup of old logs.
    /// </summary>
    /// <remarks>
    ///     Use this manager to store global add-in logs that should be persisted between Revit sessions.
    ///     There is no non-default file path, it is always `{basePath}/log.txt`
    /// </remarks>
    public static GlobalLoggingManager GlobalLogging() => new(BasePath);

    /// <summary>
    ///     Manager for the `settings\` storage dir. Handles granular read-only to JSON and CSV.
    /// </summary>
    /// <remarks>
    ///     Use this manager to store add-in settings that should be persisted between Revit sessions.
    ///     Data here should be updated but never completely overwritten (unless via an import).
    ///     The default file path is `{addinName}/settings/settings.json`
    /// </remarks>
    public SettingsManager Settings() => new(this._addinPath);

    /// <summary>
    ///     Manager for the `state\` storage dir. Handles granular read/write to CSV, and full (non-granular) read/write to
    ///     JSON.
    /// </summary>
    /// <remarks>
    ///     Use this manager to store add-in state that should be persisted between Revit sessions.
    ///     Data here is meant to be frequently granularly updated but never overwritten (unless via an import).
    ///     The default file path is `{addinName}/state/state.json`
    /// </remarks>
    public StateManager State() => new(this._addinPath);

    /// <summary>
    ///     Manager for the `output\` storage dir. Handles full (non-granular) writes to any file type.
    /// </summary>
    /// <remarks>
    ///     Use this manager to save add-in output for the user.
    ///     Data here should be written once to and then opened by the user.
    ///     There is NO DEFAULT FILE PATH for output files.
    /// </remarks>
    public OutputManager Output() => new(this._addinPath);
}