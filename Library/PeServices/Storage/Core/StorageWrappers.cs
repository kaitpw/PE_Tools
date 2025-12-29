namespace PeServices.Storage.Core;

// ============================================================
// INTERFACES - Restrictive interfaces for different operation types
// ============================================================

public interface JsonReader<T> {
    string FilePath { get; }
    T Read();
}

public interface JsonWriter<T> {
    string FilePath { get; }
    string Write(T data);
}

public interface JsonReadWriter<T> : JsonReader<T>, JsonWriter<T> where T : class, new() {
    bool IsCacheValid(int maxAgeMinutes, Func<T, bool> contentValidator = null);
}

public interface CsvReader<T> {
    string FilePath { get; }
    Dictionary<string, T> Read();
    T ReadRow(string key);
}

public interface CsvWriter<T> {
    string FilePath { get; }
    string Write(Dictionary<string, T> data);
    string WriteRow(string key, T rowData);
}

public interface CsvReadWriter<T> : CsvReader<T>, CsvWriter<T> where T : class, new() { }

// ============================================================
// JSON WRAPPERS - Thin orchestrators that compose Json<T> methods
// ============================================================

/// <summary>
///     Settings-style JSON reader: sanitizes on read, writes schema, throws if file doesn't exist.
///     Used for user-facing configuration files that should be reviewed before use.
/// </summary>
public class SettingsJsonReader<T> : JsonReader<T> where T : class, new() {
    private readonly Json<T> _json;

    public SettingsJsonReader(string filePath) {
        this._json = new Json<T>(filePath);

        if (!this._json.FileExists) {
            this._json.WriteUnvalidated(new T(), injectSchemaRef: true);
            this._json.WriteSchema();
            throw new CrashProgramException(
                $"File {filePath} did not exist. A default file was created, please review it and try again.");
        }
    }

    public string FilePath => this._json.FilePath;

    public T Read() {
        var result = this._json.ReadAndSanitize();
        this._json.WriteSchema();
        return result;
    }
}

/// <summary>
///     Dangerous JSON reader: reads without recovery, default creation, or sanitization.
///     Throws exceptions immediately for any validation errors or missing files.
///     Use for diagnostics/tooltips where you want to see raw validation errors.
/// </summary>
public class DangerousJsonReader<T> : JsonReader<T> where T : class, new() {
    private readonly Json<T> _json;

    public DangerousJsonReader(string filePath) => this._json = new Json<T>(filePath);

    public string FilePath => this._json.FilePath;

    /// <summary>
    ///     Reads and validates the JSON file without any recovery or default creation.
    ///     Throws immediately if file doesn't exist or validation fails.
    /// </summary>
    public T Read() {
        if (!this._json.FileExists)
            throw new FileNotFoundException($"JSON file not found: {this.FilePath}");

        return this._json.Read(); // Simple read with validation only
    }
}

/// <summary>
///     State-style JSON reader/writer: creates defaults silently, writes schema.
///     Used for internal state that should persist between sessions.
/// </summary>
public class StateJsonReaderWriter<T> : JsonReadWriter<T> where T : class, new() {
    private readonly Json<T> _json;

    public StateJsonReaderWriter(string filePath) => this._json = new Json<T>(filePath);

    public string FilePath => this._json.FilePath;

    public T Read() {
        var result = this._json.ReadOrCreate();
        this._json.WriteSchema();
        return result;
    }

    public string Write(T data) {
        this._json.Write(data, injectSchemaRef: true);
        this._json.WriteSchema();
        return this._json.FilePath;
    }

    public bool IsCacheValid(int maxAgeMinutes, Func<T, bool> contentValidator = null) =>
        this._json.IsCacheValid(maxAgeMinutes, contentValidator);
}

/// <summary>
///     Output-style JSON writer: write-only, no schema, no validation.
///     Used for logs, exports, and other output files.
/// </summary>
public class OutputJsonWriter<T> : JsonWriter<T> where T : class, new() {
    private readonly Json<T> _json;

    public OutputJsonWriter(string filePath) => this._json = new Json<T>(filePath);

    public string FilePath => this._json.FilePath;

    public string Write(T data) {
        this._json.WriteUnvalidated(data);
        return this._json.FilePath;
    }
}
