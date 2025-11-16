using PeServices.Storage;
using PeServices.Storage.Core;
using WpfColor = System.Windows.Media.Color;

namespace AddinPaletteSuite.Core.Services;

/// <summary>
///     Singleton service that manages color assignments for documents.
///     Each document gets a unique, persistent color used for tab colorization and UI indicators.
///     Note: No locking needed as Revit API is single-threaded.
/// </summary>
public class DocumentColorService {
    private static DocumentColorService _instance;
    private readonly CsvReadWriter<DocumentColorData> _state;
    private Dictionary<string, WpfColor> _colorCache = new();
    private readonly Random _random = new();

    private DocumentColorService() {
        var storage = new Storage(nameof(DocumentColorService));
        this._state = storage.StateDir().Csv<DocumentColorData>();
        this.LoadColors();
    }

    public static DocumentColorService Instance {
        get {
            _instance ??= new DocumentColorService();
            return _instance;
        }
    }

    /// <summary>
    ///     Gets or creates a color for the specified document.
    ///     First tries to read from Revit UI (pyRevit colorization), then falls back to generated colors.
    /// </summary>
    public WpfColor GetOrCreateDocumentColor(Document doc) {
        if (doc == null) return System.Windows.Media.Colors.Gray;

        var docKey = GetDocumentKey(doc);

        // Try to read color from Revit UI (pyRevit or other addin)
        var uiColor = RevitTabColorReader.GetDocumentColorFromUI(doc);
        if (uiColor.HasValue) {
            this._colorCache[docKey] = uiColor.Value;
            return uiColor.Value;
        }

        // No UI color found, check our cache
        if (this._colorCache.TryGetValue(docKey, out var existingColor))
            return existingColor;

        // Generate new color as last resort
        var newColor = this.GenerateVibrantColor();
        this._colorCache[docKey] = newColor;

        // Persist to storage
        var colorData = new DocumentColorData {
            DocumentKey = docKey,
            R = newColor.R,
            G = newColor.G,
            B = newColor.B
        };
        this._state.WriteRow(docKey, colorData);

        return newColor;
    }

    /// <summary>
    ///     Removes a document from the color cache when it's closed
    /// </summary>
    public void RemoveDocument(Document doc) {
        if (doc == null) return;
        var docKey = GetDocumentKey(doc);
        _ = this._colorCache.Remove(docKey);
    }

    /// <summary>
    ///     Loads persisted colors from storage
    /// </summary>
    private void LoadColors() {
        var storedColors = this._state.Read();
        foreach (var kvp in storedColors) {
            var data = kvp.Value;
            this._colorCache[kvp.Key] = WpfColor.FromRgb(data.R, data.G, data.B);
        }
    }

    /// <summary>
    ///     Generates a vibrant, distinguishable color using HSL color space
    /// </summary>
    private WpfColor GenerateVibrantColor() {
        // Generate random hue (0-360 degrees)
        var hue = this._random.Next(0, 360);

        // High saturation (70-90%) for vibrant colors
        var saturation = 0.7 + (this._random.NextDouble() * 0.2);

        // Medium lightness (45-65%) for good contrast
        var lightness = 0.45 + (this._random.NextDouble() * 0.2);

        return HslToRgb(hue, saturation, lightness);
    }

    /// <summary>
    ///     Converts HSL color values to RGB
    /// </summary>
    private static WpfColor HslToRgb(double h, double s, double l) {
        h /= 360.0;

        double r, g, b;

        if (s == 0) {
            r = g = b = l; // achromatic
        } else {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }

        return WpfColor.FromRgb(
            (byte)Math.Round(r * 255),
            (byte)Math.Round(g * 255),
            (byte)Math.Round(b * 255)
        );
    }

    private static double HueToRgb(double p, double q, double t) {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }

    private static string GetDocumentKey(Document doc) =>
        !string.IsNullOrEmpty(doc.PathName) ? doc.PathName : doc.Title;
}

/// <summary>
///     Data structure for persisting document colors
/// </summary>
public record DocumentColorData {
    public string DocumentKey { get; init; }
    public byte R { get; init; }
    public byte G { get; init; }
    public byte B { get; init; }
}

