using Json.Schema.Generation;
using PeServices.Storage.Models;
using PeUtils.Files;

namespace PeServices.Storage.Core;

public class SettingsManager {
    private readonly string _thisPath;

    public SettingsManager(string addinPath) {
        this._thisPath = Path.Combine(addinPath, "settings");
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

    public StateManager(string addinPath) {
        this._thisPath = Path.Combine(addinPath, "state");
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

    public OutputManager(string addinPath) {
        this._thisPath = Path.Combine(addinPath, "output");
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