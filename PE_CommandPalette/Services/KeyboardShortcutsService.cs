using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using PE_Lib;

namespace PE_CommandPalette.Services
{
    /// <summary>
    /// Service for parsing and managing Revit keyboard shortcuts from XML file
    /// </summary>
    public class KeyboardShortcutsService
    {
        private static readonly Lazy<KeyboardShortcutsService> _instance =
            new Lazy<KeyboardShortcutsService>(() => new KeyboardShortcutsService());

        private Dictionary<string, ShortcutInfo> _shortcuts;
        private readonly object _lockObject = new object();

        public static KeyboardShortcutsService Instance => _instance.Value;

        private KeyboardShortcutsService() { }

        /// <summary>
        /// Gets the keyboard shortcuts file path for the current Revit version
        /// </summary>
        private string GetShortcutsFilePath()
        {
            string? revitVersion = Utils.GetRevitVersion();
            if (revitVersion == null)
            {
                UiUtils.ShowBalloon("Revit version not found");
                return string.Empty;
            }
            string appDataPath = Environment.GetFolderPath(
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
        /// Loads and parses the keyboard shortcuts XML file
        /// </summary>
        public Dictionary<string, ShortcutInfo> GetShortcuts()
        {
            if (_shortcuts == null)
            {
                lock (_lockObject)
                {
                    if (_shortcuts == null)
                    {
                        _shortcuts = LoadShortcutsFromXml();
                    }
                }
            }

            return _shortcuts;
        }

        /// <summary>
        /// Gets shortcut information for a specific command ID
        /// </summary>
        public ShortcutInfo GetShortcutInfo(string commandId)
        {
            var shortcuts = GetShortcuts();
            return shortcuts.TryGetValue(commandId, out var shortcutInfo) ? shortcutInfo : null;
        }

        /// <summary>
        /// Parses the XML file and extracts shortcut information
        /// </summary>
        private Dictionary<string, ShortcutInfo> LoadShortcutsFromXml()
        {
            var shortcuts = new Dictionary<string, ShortcutInfo>(StringComparer.OrdinalIgnoreCase);
            string filePath = GetShortcutsFilePath();

            try
            {
                if (!File.Exists(filePath))
                {
                    return shortcuts; // Return empty dictionary if file doesn't exist
                }

                XDocument doc = XDocument.Load(filePath);
                var shortcutItems = doc.Descendants("ShortcutItem");

                foreach (var item in shortcutItems)
                {
                    string commandId = item.Attribute("CommandId")?.Value;
                    string commandName = item.Attribute("CommandName")?.Value;
                    string shortcutsAttr = item.Attribute("Shortcuts")?.Value;
                    string pathsAttr = item.Attribute("Paths")?.Value;

                    if (!string.IsNullOrEmpty(commandId))
                    {
                        var shortcutInfo = new ShortcutInfo
                        {
                            CommandId = commandId,
                            CommandName = DecodeHtmlEntities(commandName ?? string.Empty),
                            Shortcuts = ParseShortcuts(shortcutsAttr),
                            Paths = ParsePaths(pathsAttr),
                        };

                        shortcuts[commandId] = shortcutInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash - return empty dictionary
                System.Diagnostics.Debug.WriteLine(
                    $"Error loading keyboard shortcuts: {ex.Message}"
                );
            }

            return shortcuts;
        }

        /// <summary>
        /// Parses the shortcuts attribute into a list of shortcut strings
        /// </summary>
        private List<string> ParseShortcuts(string shortcutsAttr)
        {
            if (string.IsNullOrEmpty(shortcutsAttr))
                return new List<string>();

            return shortcutsAttr.Split('#').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        /// <summary>
        /// Parses the paths attribute into a list of path strings
        /// </summary>
        private List<string> ParsePaths(string pathsAttr)
        {
            if (string.IsNullOrEmpty(pathsAttr))
                return new List<string>();

            return pathsAttr
                .Split(';')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => DecodeHtmlEntities(s.Trim()))
                .ToList();
        }

        /// <summary>
        /// Decodes common HTML entities in the XML and ensures single-line output
        /// </summary>
        private string DecodeHtmlEntities(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Decode HTML entities and replace line breaks with a space
            string decoded = text
                .Replace("&gt;", ">")
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&quot;", "\"")
                .Replace("&#xA;", " ") // XML line break entity
                .Replace("\n", " ")
                .Replace("\r", " ");

            // Collapse multiple spaces to a single space and trim
            return System.Text.RegularExpressions.Regex.Replace(decoded, @"\s+", " ").Trim();
        }

        /// <summary>
        /// Gets a truncated display string for paths
        /// </summary>
        public string GetTruncatedPaths(List<string> paths, int maxLength = 50)
        {
            if (paths == null || paths.Count == 0)
                return string.Empty;

            string allPaths = string.Join("; ", paths);
            if (allPaths.Length <= maxLength)
                return allPaths;

            return allPaths.Substring(0, maxLength - 3) + "...";
        }

        /// <summary>
        /// Test method to verify shortcuts are loaded correctly
        /// </summary>
        public void TestShortcutsLoading()
        {
            var shortcuts = GetShortcuts();
            UiUtils.ShowBalloon($"Loaded {shortcuts.Count} keyboard shortcuts");

            // Show a few examples
            int count = 0;
            foreach (var shortcut in shortcuts.Take(5))
            {
                UiUtils.ShowBalloon(
                    $"Command: {shortcut.Value.CommandName}, ID: {shortcut.Key}, Shortcuts: {shortcut.Value.AllShortcuts}"
                );
                count++;
            }
        }
    }

    /// <summary>
    /// Represents keyboard shortcut information for a command
    /// </summary>
    public class ShortcutInfo
    {
        public string CommandId { get; set; }
        public string CommandName { get; set; }
        public List<string> Shortcuts { get; set; } = new List<string>();
        public List<string> Paths { get; set; } = new List<string>();

        /// <summary>
        /// Gets the primary shortcut as a display string
        /// </summary>
        public string PrimaryShortcut => Shortcuts.Count > 0 ? Shortcuts[0] : string.Empty;

        /// <summary>
        /// Gets all shortcuts as a display string
        /// </summary>
        public string AllShortcuts => string.Join(", ", Shortcuts);

        /// <summary>
        /// Gets all paths as a display string
        /// </summary>
        public string AllPaths => string.Join("; ", Paths);
    }
}
