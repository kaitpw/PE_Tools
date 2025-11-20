namespace AddinPaletteSuite.Core.Services;

/// <summary>
///     Singleton service that tracks view activation history for MRU (Most Recently Used) ordering
///     across all open documents.
///     Note: No locking needed as Revit API is single-threaded.
/// </summary>
public class MruViewService {
    private const int MaxBufferSize = 50;
    private static MruViewService _instance;
    private readonly List<ViewReference> _mruBuffer = new();

    private MruViewService() { }

    public static MruViewService Instance {
        get {
            _instance ??= new MruViewService();
            return _instance;
        }
    }

    /// <summary>
    ///     Records a view activation, adding it to the front of the MRU buffer
    /// </summary>
    public void RecordViewActivation(Document doc, ElementId viewId) {
        if (doc == null || viewId == null || viewId == ElementId.InvalidElementId) return;

        var viewRef = new ViewReference(doc.Title, doc.PathName, viewId);

        // Remove if already exists (will be re-added at front)
        _ = this._mruBuffer.RemoveAll(v => v.Equals(viewRef));

        // Add to front of buffer
        this._mruBuffer.Insert(0, viewRef);

        // Trim buffer if exceeds max size
        if (this._mruBuffer.Count > MaxBufferSize)
            this._mruBuffer.RemoveRange(MaxBufferSize, this._mruBuffer.Count - MaxBufferSize);
    }

    /// <summary>
    ///     Gets all views in MRU order (most recently used first) from all open documents
    ///     Only returns views that are currently open as tabs
    /// </summary>
    public IEnumerable<View> GetMruOrderedViews(UIApplication uiApp) {
        if (uiApp == null) return Enumerable.Empty<View>();

        var views = new List<View>();

        // Build a lookup of all open documents
        var openDocs = new Dictionary<string, Document>();
        foreach (Document doc in uiApp.Application.Documents) {
            var key = GetDocumentKey(doc);
            openDocs[key] = doc;
        }

        // Build a set of all currently open view IDs across all documents
        var openViewIds = new HashSet<ElementId>();
        foreach (var doc in openDocs.Values) {
            try {
                var uiDoc = new UIDocument(doc);
                var openUIViews = uiDoc.GetOpenUIViews();
                foreach (var uiView in openUIViews) _ = openViewIds.Add(uiView.ViewId);
            } catch {
                // Skip documents that can't create UIDocument (shouldn't happen for open docs)
            }
        }

        foreach (var viewRef in this._mruBuffer) {
            // Find the document for this view
            if (!openDocs.TryGetValue(viewRef.DocumentKey, out var doc)) continue;

            // Check if this view is currently open as a tab
            if (!openViewIds.Contains(viewRef.ViewId)) continue;

            // Get the view from the document
            if (doc.GetElement(viewRef.ViewId) is not View view) continue;

            views.Add(view);
        }

        return views;
    }

    /// <summary>
    ///     Removes all views from a specific document (e.g., when document is closed)
    /// </summary>
    public void RemoveDocumentViews(Document doc) {
        if (doc == null) return;

        var docKey = GetDocumentKey(doc);
        _ = this._mruBuffer.RemoveAll(v => v.DocumentKey == docKey);
    }

    /// <summary>
    ///     Clears the MRU buffer (useful for testing or reset scenarios)
    /// </summary>
    public void Clear() => this._mruBuffer.Clear();

    // Use PathName if available (saved documents), otherwise use Title
    private static string GetDocumentKey(Document doc) =>
        !string.IsNullOrEmpty(doc.PathName) ? doc.PathName : doc.Title;

    /// <summary>
    ///     Represents a reference to a view in a specific document
    /// </summary>
    private record ViewReference {
        public ViewReference(string documentTitle, string documentPath, ElementId viewId) {
            this.DocumentTitle = documentTitle;
            this.DocumentPath = documentPath;
            this.ViewId = viewId;
            // Use path if available, otherwise title
            this.DocumentKey = !string.IsNullOrEmpty(documentPath) ? documentPath : documentTitle;
        }

        public string DocumentTitle { get; }
        public string DocumentPath { get; }
        public ElementId ViewId { get; }
        public string DocumentKey { get; }

        public virtual bool Equals(ViewReference other) {
            if (other == null) return false;
            return this.DocumentKey == other.DocumentKey && this.ViewId.Equals(other.ViewId);
        }

        public override int GetHashCode() {
            unchecked {
                var hash = 17;
                hash = (hash * 31) + this.DocumentKey.GetHashCode();
                hash = (hash * 31) + this.ViewId.GetHashCode();
                return hash;
            }
        }
    }
}