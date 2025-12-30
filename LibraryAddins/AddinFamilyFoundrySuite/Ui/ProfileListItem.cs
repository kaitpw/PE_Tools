using Newtonsoft.Json.Linq;
using PeUi.Core;
using System.Windows.Media.Imaging;
using WpfColor = System.Windows.Media.Color;

namespace AddinFamilyFoundrySuite.Ui;

/// <summary>
///     Palette list item representing a Family Foundry profile JSON file.
///     Displays metadata: filename, $extends value, line count, dates.
/// </summary>
public class ProfileListItem : IPaletteListItem {
    public readonly FileInfo _fileInfo;

    public ProfileListItem(string filePath) {
        this.FilePath = filePath;
        this._fileInfo = new FileInfo(filePath);
        this.ExtendsValue = ExtractExtendsValue(filePath);
        this.LineCount = File.ReadAllLines(filePath).Length;
    }

    /// <summary> Full path to the profile JSON file </summary>
    public string FilePath { get; }

    /// <summary> Number of lines in the profile file </summary>
    public int LineCount { get; }

    /// <summary> The value of $extends property, or null if not present </summary>
    public string ExtendsValue { get; }

    /// <summary> Last modified date for sorting </summary>
    public DateTime LastModified => this._fileInfo.LastWriteTime;

    /// <summary> Profile filename without extension </summary>
    public string TextPrimary => Path.GetFileNameWithoutExtension(this.FilePath);

    /// <summary> Shows $extends value or "Base Profile" </summary>
    public string TextSecondary => string.IsNullOrEmpty(this.ExtendsValue)
        ? "Base Profile"
        : $"extends: {this.ExtendsValue}";

    /// <summary> Line count badge </summary>
    public string TextPill => $"{this.LineCount} lines";

    public Func<string> GetTextInfo => null; // Tooltip disabled - info shown in preview panel

    public BitmapImage Icon => null;
    public WpfColor? ItemColor => null;

    /// <summary>
    ///     Extracts the $extends value from a JSON file without fully parsing.
    /// </summary>
    private static string ExtractExtendsValue(string filePath) {
        try {
            var content = File.ReadAllText(filePath);
            var jObject = JObject.Parse(content);
            return jObject.TryGetValue("$extends", out var token)
                ? token.Value<string>()
                : null;
        } catch {
            return null;
        }
    }

    /// <summary>
    ///     Discovers all profile JSON files in a directory, excluding schema files.
    /// </summary>
    public static List<ProfileListItem> DiscoverProfiles(string profilesDirectory) {
        if (!Directory.Exists(profilesDirectory))
            return [];

        return Directory.GetFiles(profilesDirectory, "*.json")
            .Where(f => !f.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase))
            .Select(f => new ProfileListItem(f))
            .OrderByDescending(p => p.LastModified)
            .ToList();
    }
}