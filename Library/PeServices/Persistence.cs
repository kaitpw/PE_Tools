using System.Reflection;
using System.Text.Json;
using PeLib;

namespace PeServices;

public enum Addins {
    CommandPalette
    // add to this as we add addins
}

/// <summary>
/// Settings schema for the Command Palette addin
/// </summary>
public class CommandPaletteSettings {
    public Settings Settings { get; set; } = new();
    public List<object> Profiles { get; set; } = new(); // No profiles needed for command palette
}

public class Settings {
    public bool EnableCrossSessionPersistence { get; set; } = true;
    public bool OpenOutputFilesOnCommandFinish { get; set; } = false;
    public bool EnableLastRunLoggingToOutput { get; set; } = false;
    public bool ShowUsageCount { get; set; } = true;
    public bool ShowAllShortcuts { get; set; } = true;
}

public class Persistence {
    /// <summary> The name of the addin which is the assembly name TODO: make this dynamic</summary>
    private readonly string AddinName;
    
    /// <summary> The path to Documents/[assembly name] which is PE_Tools for now TODO: make this dynamic</summary>
    private readonly string PATH_PERSISTENCE;
    private readonly string PATH_DIR_SETTINGS;
    private readonly string PATH_DIR_STATE;
    private readonly string PATH_DIR_TEMP;
    private readonly string PATH_DIR_OUTPUT;
    private readonly string PATH_FILE_SETTINGS;
    private readonly string PATH_FILE_STATE;

    public Persistence(string addinName) {
        AddinName = addinName;
        
        // Initialize paths in constructor to avoid field initializer issues
        PATH_PERSISTENCE = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Assembly.GetExecutingAssembly().GetName().Name ?? "PE_Tools",
            AddinName);
        
        PATH_DIR_SETTINGS = Path.Combine(PATH_PERSISTENCE, "settings");
        PATH_DIR_STATE = Path.Combine(PATH_PERSISTENCE, "state");
        PATH_DIR_TEMP = Path.Combine(PATH_PERSISTENCE, "temp");
        PATH_DIR_OUTPUT = Path.Combine(PATH_PERSISTENCE, "output");
        PATH_FILE_SETTINGS = Path.Combine(PATH_DIR_SETTINGS, "settings.json");
        PATH_FILE_STATE = Path.Combine(PATH_DIR_STATE, "state.csv");
    }

    /// <summary>
    /// Ensures all required directories exist
    /// </summary>
    private void EnsureDirectoriesExist() {
        Directory.CreateDirectory(PATH_DIR_SETTINGS);
        Directory.CreateDirectory(PATH_DIR_STATE);
        Directory.CreateDirectory(PATH_DIR_TEMP);
        Directory.CreateDirectory(PATH_DIR_OUTPUT);
    }

    /// <summary>
    /// Reads settings from the default settings file
    /// </summary>
    public T? ReadSettings<T>() where T : class {
        try {
            EnsureDirectoriesExist();
            if (!File.Exists(PATH_FILE_SETTINGS)) return null;
            
            var json = File.ReadAllText(PATH_FILE_SETTINGS);
            return JsonSerializer.Deserialize<T>(json);
        } catch {
            return null;
        }
    }

    /// <summary>
    /// Writes settings to the default settings file
    /// </summary>
    public void WriteSettings<T>(T settings) where T : class {
        try {
            EnsureDirectoriesExist();
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PATH_FILE_SETTINGS, json);
        } catch {
            // TODO: Add proper error handling
        }
    }

    /// <summary>
    /// Reads state from the default state file (CSV format for command palette)
    /// </summary>
    public Dictionary<string, int> ReadState() {
        try {
            EnsureDirectoriesExist();
            if (!File.Exists(PATH_FILE_STATE)) return new Dictionary<string, int>();
            
            var state = new Dictionary<string, int>();
            var lines = File.ReadAllLines(PATH_FILE_STATE);
            
            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = line.Split(',');
                if (parts.Length >= 2 && int.TryParse(parts[1], out var score)) {
                    state[parts[0]] = score;
                }
            }
            
            return state;
        } catch {
            return new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// Writes state to the default state file (CSV format for command palette)
    /// </summary>
    public void WriteState(Dictionary<string, int> state) {
        try {
            EnsureDirectoriesExist();
            var lines = new List<string> { "CommandId,Score" };
            
            foreach (var kvp in state) {
                lines.Add($"{kvp.Key},{kvp.Value}");
            }
            
            File.WriteAllLines(PATH_FILE_STATE, lines);
        } catch {
            // TODO: Add proper error handling
        }
    }

    /// <summary>
    /// Updates the score for a specific command using CommandRef
    /// </summary>
    public void UpdateCommandScore(CommandRef commandRef, int newScore) {
        var commandId = commandRef.Value.ToString() ?? string.Empty;
        var state = ReadState();
        state[commandId] = newScore;
        WriteState(state);
    }

    /// <summary>
    /// Gets the current score for a command, or 0 if not found
    /// </summary>
    public int GetCommandScore(CommandRef commandRef) {
        var commandId = commandRef.Value.ToString() ?? string.Empty;
        var state = ReadState();
        return state.TryGetValue(commandId, out var score) ? score : 0;
    }

    // make the base settings schema here
}

