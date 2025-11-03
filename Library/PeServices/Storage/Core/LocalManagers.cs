namespace PeServices.Storage.Core;

public abstract class BaseLocalManager {
    private readonly string _directory;

    public BaseLocalManager(string parentDir, string subDirName) {
        this.Name = subDirName;
        this._directory = Path.Combine(parentDir, this.Name);
        _ = Directory.CreateDirectory(this._directory);
    }

    public abstract string Name { get; init; }

    public JsonReadWriter<T> Json<T>() where T : class, new() =>
        new(new Json<T>(Path.Combine(this._directory, $"{this.Name}.json"), false));

    public JsonReadWriter<T> Json<T>(string filename) where T : class, new() =>
        new(new Json<T>(Path.Combine(this._directory, filename), false));

    public CsvReadWriter<T> Csv<T>() where T : class, new() =>
        new(new Csv<T>(Path.Combine(this._directory, "state.csv")));

    public CsvReadWriter<T> Csv<T>(string filename) where T : class, new() =>
        new(new Csv<T>(Path.Combine(this._directory, filename)));
}

public class SettingsManager {
    private readonly string _directory;

    public SettingsManager(string parentPath) {
        this._directory = Path.Combine(parentPath, this.Name);
        _ = Directory.CreateDirectory(this._directory);
    }

    private SettingsManager(string parentPath, string name) {
        this._directory = parentPath;
        this.Name = name;
        _ = Directory.CreateDirectory(this._directory);
    }

    public string Name { get; internal set; } = "settings";

    public JsonReader<T> Json<T>(bool throwIfNotExists = true) where T : class, new() =>
        new(new Json<T>(Path.Combine(this._directory, $"{this.Name}.json"), throwIfNotExists));

    public JsonReader<T> Json<T>(string filename, bool throwIfNotExists = true) where T : class, new() {
        var filePath = Path.Combine(this._directory, filename);
        // Ensure subdirectories exist if filename contains path separators
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory)) _ = Directory.CreateDirectory(directory);

        return new JsonReader<T>(new Json<T>(filePath, throwIfNotExists));
    }

    public string GetFolderPath() => this._directory;

    /// <summary>
    ///     Navigate to a subdirectory for accessing files within nested folders.
    ///     Supports multi-level nesting via chaining or path strings (e.g., "profiles/production").
    /// </summary>
    public SettingsManager Subdirectory(string subdirectory) {
        var subdirectoryPath = Path.Combine(this._directory, subdirectory);
        // Validate path doesn't escape base directory
        if (Path.GetFullPath(subdirectoryPath)
            .StartsWith(Path.GetFullPath(this._directory)))
            return new SettingsManager(subdirectoryPath, this.Name);

        throw new ArgumentException($"Subdirectory path '{subdirectory}' would escape base directory.");
    }
}

public class StateManager {
    private readonly string _directory;

    public StateManager(string addinPath) {
        this._directory = Path.Combine(addinPath, "state");
        _ = Directory.CreateDirectory(this._directory);
    }

    public JsonReadWriter<T> Json<T>() where T : class, new() =>
        new(new Json<T>(Path.Combine(this._directory, "state.json"), false));

    public JsonReadWriter<T> Json<T>(string filename) where T : class, new() =>
        new(new Json<T>(Path.Combine(this._directory, filename), false));

    public CsvReadWriter<T> Csv<T>() where T : class, new() =>
        new(new Csv<T>(Path.Combine(this._directory, "state.csv")));

    public CsvReadWriter<T> Csv<T>(string filename) where T : class, new() =>
        new(new Csv<T>(Path.Combine(this._directory, filename)));
}

public class OutputManager {
    private readonly string _directory;

    public OutputManager(string parentPath) {
        this._directory = Path.Combine(parentPath, this.Name);
        _ = Directory.CreateDirectory(this._directory);
    }

    private OutputManager(string fullPath, string name) {
        this._directory = fullPath;
        this.Name = name;
        _ = Directory.CreateDirectory(this._directory);
    }

    public string Name { get; internal set; } = "output";

    public string GetFolderPath() => this._directory;

    public JsonWriter<T> Json<T>(string filename) where T : class, new() {
        var filePath = Path.Combine(this._directory, filename);
        // Ensure subdirectories exist if filename contains path separators
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory)) _ = Directory.CreateDirectory(directory);

        return new JsonWriter<T>(new Json<T>(filePath, false, true), true, true);
    }

    public CsvWriter<T> Csv<T>(string filename) where T : class, new() {
        var filePath = Path.Combine(this._directory, filename);
        // Ensure subdirectories exist if filename contains path separators
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory)) _ = Directory.CreateDirectory(directory);

        return new CsvWriter<T>(new Csv<T>(filePath));
    }

    /// <summary>
    ///     Navigate to a subdirectory for accessing files within nested folders.
    ///     Supports multi-level nesting via chaining or path strings (e.g., "reports/2024").
    /// </summary>
    public OutputManager Subdirectory(string subdirectory) {
        var subdirectoryPath = Path.Combine(this._directory, subdirectory);
        // Validate path doesn't escape base directory
        if (Path.GetFullPath(subdirectoryPath).StartsWith(Path.GetFullPath(this._directory)))
            return new OutputManager(subdirectoryPath, this.Name);

        throw new ArgumentException($"Subdirectory path '{subdirectory}' would escape base directory.");
    }
}