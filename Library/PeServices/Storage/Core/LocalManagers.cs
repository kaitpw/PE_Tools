using PeUtils.Files;
using System.ComponentModel;

namespace PeServices.Storage.Core;

public class SettingsManager {
    private readonly string _thisPath;

    public SettingsManager(string addinPath) {
        this._thisPath = Path.Combine(addinPath, "settings");
        _ = Directory.CreateDirectory(this._thisPath);
    }

    public JsonReader<T> Json<T>(bool throwIfNotExists = true) where T : class, new() =>
        new(new Json<T>(Path.Combine(this._thisPath, "settings.json"), throwIfNotExists));

    public JsonReader<T> Json<T>(string filename, bool throwIfNotExists = true) where T : class, new() =>
        new(new Json<T>(Path.Combine(this._thisPath, filename), throwIfNotExists));
}

public class StateManager {
    private readonly string _thisPath;

    public StateManager(string addinPath) {
        this._thisPath = Path.Combine(addinPath, "state");
        _ = Directory.CreateDirectory(this._thisPath);
    }

    public JsonReadWriter<T> Json<T>() where T : class, new() =>
        new(new Json<T>(Path.Combine(this._thisPath, "state.json"), false));

    public JsonReadWriter<T> Json<T>(string filename) where T : class, new() =>
        new(new Json<T>(Path.Combine(this._thisPath, filename), false));

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
        new(new Json<T>(Path.Combine(this._thisPath, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json"), false));

    public JsonWriter<T> Json<T>(string filename) where T : class, new() =>
        new(new Json<T>(Path.Combine(this._thisPath, filename), false));

    public CsvWriter<T> Csv<T>() where T : class, new() =>
        new(new Csv<T>(Path.Combine(this._thisPath, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv")));

    public CsvWriter<T> Csv<T>(string filename) where T : class, new() =>
        new(new Csv<T>(Path.Combine(this._thisPath, filename)));
}