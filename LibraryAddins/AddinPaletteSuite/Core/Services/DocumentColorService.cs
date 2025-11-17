using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace AddinPaletteSuite.Core.Services;

/// <summary>
///     Singleton service that provides color assignments for documents.
///     Colors are read from Revit UI (pyRevit tab colors) and cached in-memory for the session.
///     Cache is cleared when documents are closed to handle color reassignment.
///     Note: No locking needed as Revit API is single-threaded.
/// </summary>
public class DocumentColorService {
    private static DocumentColorService _instance;
    private readonly Dictionary<string, WpfColor> _colorCache = new();
    private readonly Random _random = new();

    private DocumentColorService() { }

    public static DocumentColorService Instance {
        get {
            _instance ??= new DocumentColorService();
            return _instance;
        }
    }

    /// <summary>
    ///     Gets the color for the specified document from cache or by reading from Revit UI.
    ///     Falls back to generating a new color if UI read fails.
    /// </summary>
    public WpfColor GetOrCreateDocumentColor(Document doc) {
        if (doc == null) return Colors.Gray;

        var docKey = GetDocumentKey(doc);

        // Check cache first
        if (this._colorCache.TryGetValue(docKey, out var cachedColor)) return cachedColor;

        // Try reading from Revit UI
        var uiColor = RevitTabColorReader.GetDocumentColorFromUI(doc);
        if (uiColor.HasValue) {
            this._colorCache[docKey] = uiColor.Value;
            return uiColor.Value;
        }

        // Fallback to generated color
        var newColor = this.GenerateVibrantColor();
        this._colorCache[docKey] = newColor;
        return newColor;
    }

    /// <summary>
    ///     Removes a document from the color cache when it's closed.
    ///     This allows colors to be reassigned if the document is reopened.
    /// </summary>
    public void RemoveDocument(Document doc) {
        if (doc == null) return;
        var docKey = GetDocumentKey(doc);
        this._colorCache.Remove(docKey);
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

        if (s == 0)
            r = g = b = l; // achromatic
        else {
            var q = l < 0.5 ? l * (1 + s) : l + s - (l * s);
            var p = (2 * l) - q;
            r = HueToRgb(p, q, h + (1.0 / 3.0));
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - (1.0 / 3.0));
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
        if (t < 1.0 / 6.0) return p + ((q - p) * 6 * t);
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + ((q - p) * ((2.0 / 3.0) - t) * 6);
        return p;
    }

    private static string GetDocumentKey(Document doc) =>
        !string.IsNullOrEmpty(doc.PathName) ? doc.PathName : doc.Title;
}