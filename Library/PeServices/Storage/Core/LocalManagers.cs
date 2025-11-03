namespace PeServices.Storage.Core;

public abstract class BaseLocalManager {
    protected BaseLocalManager(string parentDir, string subDirName) {
        this.Name = subDirName;
        this.DirectoryPath = Path.Combine(parentDir, this.Name);
        _ = Directory.CreateDirectory(this.DirectoryPath);
    }

    public abstract string Name { get; init; }
    public abstract bool ThrowIfDefaultCreated { get; }
    public abstract bool SaveSchema { get; }
    public string DirectoryPath { get; init; }

    /// <summary>
    ///     Get the path to the JSON file. Uses the <see cref="Name" /> of the manager by default.
    /// </summary>
    public string GetJsonPath(string filename = null) =>
        Path.Combine(this.DirectoryPath, filename ?? $"{this.Name}.json");

    /// <summary>
    ///     Get the path to the CSV file. Uses the <see cref="Name" /> of the manager by default.
    /// </summary>
    public string GetCsvPath(string filename = null) =>
        Path.Combine(this.DirectoryPath, filename ?? $"{this.Name}.csv");

}

public class SettingsManager : BaseLocalManager {
    private const string defaultName = "settings";
    public SettingsManager(string parentPath) : base(parentPath, defaultName) { }
    private SettingsManager(string parentPath, string subDirName) : base(parentPath, subDirName) { }
    public override bool ThrowIfDefaultCreated { get; } = true;
    public override bool SaveSchema { get; } = true;
    public override string Name { get; init; } = defaultName;

    public JsonReader<T> Json<T>() where T : class, new() =>
        new Json<T>(this.GetJsonPath(), this.ThrowIfDefaultCreated, this.SaveSchema);

    public JsonReader<T> Json<T>(string filename) where T : class, new() =>
        new Json<T>(this.GetJsonPath(filename), this.ThrowIfDefaultCreated, this.SaveSchema);

    /// <summary>
    ///     Navigate to a subdirectory for accessing files within nested folders.
    ///     Supports multi-level nesting via chaining or path strings (e.g., "profiles/production").
    /// </summary>
    public SettingsManager Subdirectory(string subdirectory) {
        var subdirectoryPath = Path.Combine(this.DirectoryPath, subdirectory);
        // Validate path doesn't escape base directory
        if (Path.GetFullPath(subdirectoryPath).StartsWith(Path.GetFullPath(this.DirectoryPath)))
            return new SettingsManager(subdirectoryPath, this.Name);

        throw new ArgumentException($"Subdirectory path '{subdirectory}' would escape base directory.");
    }
}

public class StateManager : BaseLocalManager {
    private const string defaultName = "state";
    public StateManager(string parentPath) : base(parentPath, defaultName) { }
    public override bool ThrowIfDefaultCreated { get; } = false;
    public override bool SaveSchema { get; } = true;
    public override string Name { get; init; } = defaultName;

    public JsonReadWriter<T> Json<T>() where T : class, new() =>
        new Json<T>(this.GetJsonPath(), this.ThrowIfDefaultCreated, this.SaveSchema);

    public JsonReadWriter<T> Json<T>(string filename) where T : class, new() =>
        new Json<T>(this.GetJsonPath(filename), this.ThrowIfDefaultCreated, this.SaveSchema);

    public CsvReadWriter<T> Csv<T>() where T : class, new() =>
        new Csv<T>(this.GetCsvPath());

    public CsvReadWriter<T> Csv<T>(string filename) where T : class, new() =>
        new Csv<T>(this.GetCsvPath(filename));
}

public class OutputManager : BaseLocalManager {
    public OutputManager(string parentPath) : base(parentPath, "output") { }
    private OutputManager(string parentPath, string subDirName) : base(parentPath, subDirName) { }
    public override string Name { get; init; } = "output";
    public override bool ThrowIfDefaultCreated { get; } = false;
    public override bool SaveSchema { get; } = false;

    public JsonWriter<T> Json<T>(string filename) where T : class, new() =>
        new Json<T>(this.GetJsonPath(filename), this.ThrowIfDefaultCreated, this.SaveSchema);

    public CsvWriter<T> Csv<T>(string filename) where T : class, new() =>
        new Csv<T>(this.GetCsvPath(filename));

    /// <summary>
    ///     Navigate to a subdirectory for accessing files within nested folders.
    ///     Supports multi-level nesting via chaining or path strings (e.g., "reports/2024").
    /// </summary>
    public OutputManager Subdirectory(string subdirectory) {
        var subdirectoryPath = Path.Combine(this.DirectoryPath, subdirectory);
        // Validate path doesn't escape base directory
        if (Path.GetFullPath(subdirectoryPath).StartsWith(Path.GetFullPath(this.DirectoryPath)))
            return new OutputManager(subdirectoryPath, this.Name);

        throw new ArgumentException($"Subdirectory path '{subdirectory}' would escape base directory.");
    }
}