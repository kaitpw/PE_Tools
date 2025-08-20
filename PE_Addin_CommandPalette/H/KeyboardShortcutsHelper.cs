using PeLib;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PE_Addin_CommandPalette.H;

/// <summary>
///     Service for parsing and managing Revit keyboard shortcuts from XML file
/// </summary>
public class KeyboardShortcutsHelper {
    private static readonly Lazy<KeyboardShortcutsHelper> _instance = new(() => new KeyboardShortcutsHelper());

    private readonly object _lockObject = new();

    private Dictionary<string, ShortcutInfo> _shortcuts;

    private KeyboardShortcutsHelper() { }

    public static KeyboardShortcutsHelper Instance => _instance.Value;

    /// <summary>
    ///     Gets the keyboard shortcuts file path for the current Revit version
    /// </summary>
    private string GetShortcutsFilePath() {
        var revitVersion = Utils.GetRevitVersion();
        if (revitVersion == null) {
            UiUtils.ShowBalloon("Revit version not found");
            return string.Empty;
        }

        var appDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData
        );
        return Path.Combine(
            appDataPath,
            "Autodesk",
            "Revit",
            $"Autodesk Revit {revitVersion}",
            "KeyboardShortcuts.xml"
        );
    }

    /// <summary>
    ///     Loads and parses the keyboard shortcuts XML file
    /// </summary>
    public Dictionary<string, ShortcutInfo> GetShortcuts() {
        if (this._shortcuts == null) {
            lock (this._lockObject) {
                if (this._shortcuts == null)
                    this._shortcuts = this.LoadShortcutsFromXml();
            }
        }

        return this._shortcuts;
    }

    /// <summary>
    ///     Gets shortcut information for a specific command ID
    /// </summary>
    public ShortcutInfo GetShortcutInfo(string commandId) {
        var shortcuts = this.GetShortcuts();
        return shortcuts.TryGetValue(commandId, out var shortcutInfo) ? shortcutInfo : null;
    }

    /// <summary>
    ///     Parses the XML file and extracts shortcut information
    /// </summary>
    private Dictionary<string, ShortcutInfo> LoadShortcutsFromXml() {
        var shortcuts = new Dictionary<string, ShortcutInfo>(StringComparer.OrdinalIgnoreCase);
        var filePath = this.GetShortcutsFilePath();

        try {
            if (!File.Exists(filePath)) return shortcuts; // Return empty dictionary if file doesn't exist

            var doc = XDocument.Load(filePath);
            var shortcutItems = doc.Descendants("ShortcutItem");

            foreach (var item in shortcutItems) {
                var commandId = item.Attribute("CommandId")?.Value;
                var commandName = item.Attribute("CommandName")?.Value;
                var shortcutsAttr = item.Attribute("Shortcuts")?.Value;
                var pathsAttr = item.Attribute("Paths")?.Value;

                if (!string.IsNullOrEmpty(commandId)) {
                    var shortcutInfo = new ShortcutInfo {
                        CommandId = commandId,
                        CommandName = this.DecodeHtmlEntities(commandName ?? string.Empty),
                        Shortcuts = this.ParseShortcuts(shortcutsAttr),
                        Paths = this.ParsePaths(pathsAttr)
                    };

                    shortcuts[commandId] = shortcutInfo;
                }
            }
        } catch (Exception ex) {
            // Log error but don't crash - return empty dictionary
            Debug.WriteLine(
                $"Error loading keyboard shortcuts: {ex.Message}"
            );
        }

        return shortcuts;
    }

    /// <summary>
    ///     Parses the shortcuts attribute into a list of shortcut strings
    /// </summary>
    private List<string> ParseShortcuts(string shortcutsAttr) {
        if (string.IsNullOrEmpty(shortcutsAttr))
            return new List<string>();

        return shortcutsAttr.Split('#').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    /// <summary>
    ///     Parses the paths attribute into a list of path strings
    /// </summary>
    private List<string> ParsePaths(string pathsAttr) {
        if (string.IsNullOrEmpty(pathsAttr))
            return new List<string>();

        return pathsAttr
            .Split(';')
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => this.DecodeHtmlEntities(s.Trim()))
            .ToList();
    }

    /// <summary>
    ///     Decodes common HTML entities in the XML and ensures single-line output
    /// </summary>
    private string DecodeHtmlEntities(string text) {
        if (string.IsNullOrEmpty(text))
            return text;

        // Decode HTML entities and replace line breaks with a space
        var decoded = text.Replace("&gt;", ">")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&quot;", "\"")
            .Replace("&#xA;", " ") // XML line break entity
            .Replace("\n", " ")
            .Replace("\r", " ");

        // Collapse multiple spaces to a single space and trim
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    /// <summary>
    ///     Gets a truncated display string for paths
    /// </summary>
    public string GetTruncatedPaths(List<string> paths, int maxLength = 50) {
        if (paths == null || paths.Count == 0)
            return string.Empty;

        var allPaths = string.Join("; ", paths);
        if (allPaths.Length <= maxLength)
            return allPaths;

        return allPaths.Substring(0, maxLength - 3) + "...";
    }
}

/// <summary>
///     Represents keyboard shortcut information for a command
/// </summary>
public class ShortcutInfo {
    public string CommandId { get; set; }
    public string CommandName { get; set; }
    public List<string> Shortcuts { get; set; } = new();
    public List<string> Paths { get; set; } = new();

    /// <summary>
    ///     Gets the primary shortcut as a display string
    /// </summary>
    public string PrimaryShortcut => this.Shortcuts.Count > 0 ? this.Shortcuts[0] : string.Empty;

    /// <summary>
    ///     Gets all shortcuts as a display string
    /// </summary>
    public string AllShortcuts => string.Join(", ", this.Shortcuts);

    /// <summary>
    ///     Gets all paths as a display string
    /// </summary>
    public string AllPaths => string.Join("; ", this.Paths);
}