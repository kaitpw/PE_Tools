namespace PeServices.Storage.Core;

public abstract class BaseLocalManager {
    protected BaseLocalManager(string parentDir, string subDirName) {
        this.Name = subDirName;
        this.DirectoryPath = Path.Combine(parentDir, this.Name);
        _ = Directory.CreateDirectory(this.DirectoryPath);
    }

    public abstract string Name { get; init; }
    public string DirectoryPath { get; init; }

    /// <summary>
    ///     Get the path to the JSON file. Uses the <see cref="Name" /> of the manager by default.
    /// </summary>
    public string GetJsonPath(string filename = null) =>
        Path.Combine(this.DirectoryPath, filename ?? $"{this.Name}.json");

    /// <summary>
    ///     Get the path to the JSON file with a timestamp in the filename. Uses the <see cref="Name" /> of the manager by
    ///     default.
    /// </summary>
    public string GetDatedJsonPath(string filename = null) =>
        Path.Combine(this.DirectoryPath,
            $"{filename ?? this.Name}_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.json");

    /// <summary>
    ///     Get the path to the CSV file. Uses the <see cref="Name" /> of the manager by default.
    /// </summary>
    public string GetCsvPath(string filename = null) =>
        Path.Combine(this.DirectoryPath, filename ?? $"{this.Name}.csv");

    /// <summary>
    ///     Get the path to the CSV file with a timestamp in the filename. Uses the <see cref="Name" /> of the manager by
    ///     default.
    /// </summary>
    public string GetDatedCsvPath(string filename = null) =>
        Path.Combine(this.DirectoryPath, $"{filename ?? this.Name}_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.csv");
}

public class SettingsManager : BaseLocalManager {
    private const string defaultName = "settings";
    public SettingsManager(string parentPath) : base(parentPath, defaultName) { }
    private SettingsManager(string parentPath, string subDirName) : base(parentPath, subDirName) { }
    public override string Name { get; init; } = defaultName;

    public JsonReader<T> Json<T>() where T : class, new() =>
        new SettingsJsonReader<T>(this.GetJsonPath());

    public JsonReader<T> Json<T>(string filename) where T : class, new() =>
        new SettingsJsonReader<T>(this.GetJsonPath(filename));

    /// <summary>
    ///     Creates a JSON reader that supports the <c>$extends</c> inheritance pattern.
    ///     Use this for profile files that may extend other profiles in the same directory.
    /// </summary>
    /// <remarks>
    ///     <para>If the JSON file contains <c>"$extends": "BaseName"</c>, it will:</para>
    ///     <list type="number">
    ///         <item>Load the base profile (with full recovery/validation)</item>
    ///         <item>Merge the child's overrides on top</item>
    ///         <item>Validate the merged result</item>
    ///     </list>
    ///     <para>Child files remain sparse - only overrides are stored.</para>
    /// </remarks>
    public JsonReader<T> JsonWithExtends<T>(string filename) where T : class, new() =>
        new JsonWithExtends<T>(this.DirectoryPath, filename);

    /// <summary>
    ///     Creates a dangerous JSON reader that reads without recovery or default creation.
    ///     Throws exceptions immediately if the file is invalid or missing.
    ///     Use for diagnostics/tooltips where you want to see raw validation errors.
    /// </summary>
    public DangerousJsonReader<T> JsonDangerous<T>(string filename) where T : class, new() =>
        new(this.GetJsonPath(filename));

    /// <summary>
    ///     Navigate to a subdirectory for accessing files within nested folders.
    ///     Supports multi-level nesting via chaining or path strings (e.g., "profiles/production").
    /// </summary>
    public SettingsManager SubDir(string subdirectory) {
        var subdirectoryPath = Path.Combine(this.DirectoryPath, subdirectory);
        // Validate path doesn't escape base directory
        if (Path.GetFullPath(subdirectoryPath).StartsWith(Path.GetFullPath(this.DirectoryPath)))
            return new SettingsManager(this.DirectoryPath, subdirectory);

        throw new ArgumentException($"Subdirectory path '{subdirectory}' would escape base directory.");
    }
}

public class StateManager : BaseLocalManager {
    private const string defaultName = "state";
    public StateManager(string parentPath) : base(parentPath, defaultName) { }
    public override string Name { get; init; } = defaultName;

    public JsonReadWriter<T> Json<T>() where T : class, new() =>
        new StateJsonReaderWriter<T>(this.GetJsonPath());

    public JsonReadWriter<T> Json<T>(string filename) where T : class, new() =>
        new StateJsonReaderWriter<T>(this.GetJsonPath(filename));

    public CsvReadWriter<T> Csv<T>() where T : class, new() =>
        new Csv<T>(this.GetCsvPath());

    public CsvReadWriter<T> Csv<T>(string filename) where T : class, new() =>
        new Csv<T>(this.GetCsvPath(filename));
}

public class OutputManager : BaseLocalManager {
    public OutputManager(string parentPath) : base(parentPath, "output") { }
    private OutputManager(string parentPath, string subDirName) : base(parentPath, subDirName) { }
    public override string Name { get; init; } = "output";

    public JsonWriter<T> Json<T>(string filename) where T : class, new() =>
        new OutputJsonWriter<T>(this.GetDatedJsonPath(filename));

    public CsvWriter<T> Csv<T>(string filename) where T : class, new() =>
        new Csv<T>(this.GetDatedCsvPath(filename));

    /// <summary>
    ///     Navigate to a subdirectory for accessing files within nested folders.
    ///     Supports multi-level nesting via chaining or path strings (e.g., "reports/2024").
    /// </summary>
    public OutputManager SubDir(string subdirectory) {
        var subdirectoryPath = Path.Combine(this.DirectoryPath, subdirectory);
        // Validate path doesn't escape base directory
        if (Path.GetFullPath(subdirectoryPath).StartsWith(Path.GetFullPath(this.DirectoryPath)))
            return new OutputManager(this.DirectoryPath, subdirectory);

        throw new ArgumentException($"Subdirectory path '{subdirectory}' would escape base directory.");
    }
}