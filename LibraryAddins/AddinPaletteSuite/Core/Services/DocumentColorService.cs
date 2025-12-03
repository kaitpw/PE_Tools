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

    /// <summary>
    ///     Fallback color used when UI read fails. Using a consistent color
    ///     makes it obvious when color detection isn't working.
    /// </summary>
    private static readonly WpfColor FallbackColor = Colors.DimGray;

    private DocumentColorService() { }

    public static DocumentColorService Instance {
        get {
            _instance ??= new DocumentColorService();
            return _instance;
        }
    }

    /// <summary>
    ///     Gets the color for the specified document from cache or by reading from Revit UI.
    ///     Falls back to DimGray if UI read fails (makes detection failures obvious).
    /// </summary>
    public WpfColor GetOrCreateDocumentColor(Document doc) {
        if (doc == null) {
            Debug.WriteLine("[DocColorSvc] GetOrCreateDocumentColor: doc is null, returning Gray");
            return Colors.Gray;
        }

        var docKey = GetDocumentKey(doc);
        Debug.WriteLine($"[DocColorSvc] GetOrCreateDocumentColor: docKey='{docKey}', Title='{doc.Title}', PathName='{doc.PathName}'");

        // Check cache first
        if (this._colorCache.TryGetValue(docKey, out var cachedColor)) {
            Debug.WriteLine($"[DocColorSvc] CACHE HIT: docKey='{docKey}' -> #{cachedColor.R:X2}{cachedColor.G:X2}{cachedColor.B:X2}");
            return cachedColor;
        }

        Debug.WriteLine($"[DocColorSvc] CACHE MISS: docKey='{docKey}', attempting UI read...");

        // Try reading from Revit UI
        var uiColor = RevitTabColorReader.GetDocumentColorFromUI(doc);
        if (uiColor.HasValue) {
            this._colorCache[docKey] = uiColor.Value;
            Debug.WriteLine($"[DocColorSvc] UI READ SUCCESS: docKey='{docKey}' -> #{uiColor.Value.R:X2}{uiColor.Value.G:X2}{uiColor.Value.B:X2}");
            return uiColor.Value;
        }

        // Fallback to consistent gray - makes it obvious when color detection fails
        Debug.WriteLine($"[DocColorSvc] UI READ FAILED: docKey='{docKey}', using fallback color DimGray");
        this._colorCache[docKey] = FallbackColor;
        return FallbackColor;
    }

    /// <summary>
    ///     Removes a document from the color cache when it's closed.
    ///     This allows colors to be reassigned if the document is reopened.
    /// </summary>
    public void RemoveDocument(Document doc) {
        if (doc == null) {
            Debug.WriteLine("[DocColorSvc] RemoveDocument: doc is null, ignoring");
            return;
        }

        var docKey = GetDocumentKey(doc);
        var existed = this._colorCache.Remove(docKey);
        Debug.WriteLine($"[DocColorSvc] RemoveDocument: docKey='{docKey}', wasInCache={existed}, cacheCount={this._colorCache.Count}");
    }

    /// <summary>
    ///     Debug helper to dump current cache state
    /// </summary>
    public void DumpCacheState() {
        Debug.WriteLine($"[DocColorSvc] === CACHE STATE ({this._colorCache.Count} entries) ===");
        foreach (var kvp in this._colorCache)
            Debug.WriteLine($"[DocColorSvc]   '{kvp.Key}' -> #{kvp.Value.R:X2}{kvp.Value.G:X2}{kvp.Value.B:X2}");
        Debug.WriteLine("[DocColorSvc] === END CACHE STATE ===");
    }

    private static string GetDocumentKey(Document doc) =>
        !string.IsNullOrEmpty(doc.PathName) ? doc.PathName : doc.Title;
}