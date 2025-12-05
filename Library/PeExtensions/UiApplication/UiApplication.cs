namespace PeExtensions.UiApplication;

public class DocumentManager(UIApplication uiApp) {
    public UIApplication UiApp => uiApp;

    // Active document properties
    public Document ActiveDoc => uiApp.ActiveUIDocument?.Document;
    public View ActiveView => uiApp.ActiveUIDocument?.ActiveView;
    public ElementId ActiveViewId => uiApp.ActiveUIDocument?.ActiveView?.Id;

    // Open documents properties
    public IEnumerable<Document> OpenDocs => uiApp.Application.Documents.Cast<Document>();

    public IEnumerable<UIView> OpenUiViews => this.OpenDocs.SelectMany(d => {
        try {
            var uiDoc = new UIDocument(d);
            return uiDoc.GetOpenUIViews();
        } catch {
            return [];
        }
    });

    public IEnumerable<ElementId> OpenViewIds => this.OpenUiViews.Select(v => v.ViewId);

    public bool IsDocOpen(Document doc) => this.OpenDocs.Any(d => d.Title == doc.Title);

    public bool IsDocActive(Document doc) => this.ActiveDoc?.Title == doc.Title;


    /// <summary>
    /// Finds an open family document matching the given Family.
    /// (partial match on Title because title is the file name (ie. "Building.rvt" or "Family.rfa").
    /// </summary>
    public Document FindOpenFamilyDocument(Family family) =>
        this.OpenDocs.FirstOrDefault(d => d.IsFamilyDocument && d.Title.Contains(family.Name));

    public ModelPath GetDocumentModelPath(Document doc) =>
        doc switch {
            { IsModelInCloud: true } => doc.GetCloudModelPath(),
            { PathName.Length: > 0 } => new FilePath(doc.PathName),
            _ => null // Return null for unsaved documents (like newly opened family docs)
        };

    public string LogDocumentState(View view = null, string context = null) {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(context)) _ = sb.AppendLine($"=== {context} ===");
        _ = sb.AppendLine("=== Document State ===");
        if (view != null) {
            _ = sb.AppendLine($"Target Document: {view.Document.Title} (Path: {view.Document.PathName})")
                .AppendLine($"Target View: {view.Name} (ID: {view.Id.Value})");
        }
        _ = sb.AppendLine($"Active Document: {this.ActiveDoc?.Title ?? "None"} (Path: {this.ActiveDoc?.PathName ?? "N/A"})")
            .AppendLine($"Active View: {this.ActiveView?.Name ?? "None"} (ID: {this.ActiveViewId?.Value ?? -1})")
            .AppendLine(
                $"Open Documents ({this.OpenDocs.Count()}): {string.Join("\n  - ", this.OpenDocs.Select(d => $"{d.Title} (Path: {d.PathName})"))}")
            .AppendLine(
                $"Open Views ({this.OpenUiViews.Count()}): {string.Join("\n  - ", this.OpenUiViews.Select(v => v.ViewId.Value))}");
        return sb.ToString();
    }
}

public static class OpenDocumentExtensions {
    /// <summary>
    /// Opens and activates a view, handling cross-document navigation.
    /// 
    /// Key API behaviors:
    /// - UIDocument.ActiveView setter ONLY works on the currently active document
    /// - UIDocument.ShowElements() is the only way to switch to a non-active document
    /// - ShowElements activates both the document AND shows the elements/view
    /// </summary>
    public static void OpenAndActivateView(this UIApplication uiApp, View targetView) {
        var docManager = new DocumentManager(uiApp);
        Debug.WriteLine(docManager.LogDocumentState(targetView, "OpenAndActivateView START"));

        try {
            var targetDoc = targetView.Document;
            var targetUiDoc = new UIDocument(targetDoc);

            // CASE 1: Target document is already the active document
            // Use RequestViewChange for reliable view switching from modeless windows
            // (ActiveView setter doesn't stick when called from palette callbacks)
            if (docManager.IsDocActive(targetDoc)) {
                Debug.WriteLine($"[OpenAndActivateView] Document '{targetDoc.Title}' is active, using RequestViewChange");
                targetUiDoc.RequestViewChange(targetView);
                return;
            }

            // CASE 2: Target document is open but not active
            if (docManager.IsDocOpen(targetDoc)) {
                Debug.WriteLine($"[OpenAndActivateView] Document '{targetDoc.Title}' is open but not active");

                // For family documents, use the special family activation (saves to temp)
                // This is required because EditFamily creates an independent copy that can't be "re-opened"
                if (targetDoc.IsFamilyDocument) {
                    Debug.WriteLine($"[OpenAndActivateView] Target is family document, using family activation...");
                    ActivateOpenFamilyDocumentAndView(uiApp, targetDoc, targetView);
                    return;
                }

                // For project documents, try OpenAndActivateDocument with the path
                var existingDocPath = docManager.GetDocumentModelPath(targetDoc);
                if (existingDocPath != null) {
                    Debug.WriteLine($"[OpenAndActivateView] Using OpenAndActivateDocument with path");
                    var existingDocOptions = new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DoNotDetach };
                    var activatedUiDoc = uiApp.OpenAndActivateDocument(existingDocPath, existingDocOptions, false);
                    // Use RequestViewChange for reliable view switching from modeless windows
                    activatedUiDoc.RequestViewChange(targetView);
                    return;
                }

                // Fallback: ShowElements (less reliable but last resort)
                Debug.WriteLine($"[OpenAndActivateView] No path available, falling back to ShowElements");
                var elementInView = FindElementInView(targetDoc, targetView);
                if (elementInView != null) {
                    Debug.WriteLine($"[OpenAndActivateView] Found element to show: {elementInView.GetType().Name} (Id: {elementInView.Id.Value})");
                    targetUiDoc.ShowElements(elementInView);
                }
                return;
            }

            // CASE 3: Document not open - try to open it via file path
            var newDocPath = docManager.GetDocumentModelPath(targetDoc);
            if (newDocPath == null) {
                Debug.WriteLine($"[OpenAndActivateView] Cannot open document '{targetDoc.Title}' - no valid path");
                return;
            }

            Debug.WriteLine($"[OpenAndActivateView] Opening document from path: {newDocPath}");
            var newDocOptions = new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DoNotDetach };
            var openedUiDoc = uiApp.OpenAndActivateDocument(newDocPath, newDocOptions, false);
            // Use RequestViewChange for reliable view switching from modeless windows
            openedUiDoc.RequestViewChange(targetView);
        } catch (Exception ex) {
            Debug.WriteLine(docManager.LogDocumentState(targetView, "OpenAndActivateView ERROR"));
            Debug.WriteLine(ex.ToStringDemystified());
        }
    }

    /// <summary>
    /// Opens and activates a family document for editing.
    /// 
    /// Logic flow:
    /// 1. If family doc is already open AND active -> do nothing (already there)
    /// 2. If family doc is already open but NOT active -> switch to it via temp file
    /// 3. If family doc is NOT open -> use EditFamily to open it, then ShowElements to activate
    /// 
    /// Note: EditFamily opens the family AS IT EXISTS IN THE PROJECT (correct behavior).
    /// ShowElements works for fresh documents but is unreliable for already-open ones.
    /// </summary>
    public static void OpenAndActivateFamily(this UIApplication uiApp, Family family) {
        var docManager = new DocumentManager(uiApp);
        Debug.WriteLine(docManager.LogDocumentState(context: "OpenAndActivateFamily START"));

        try {
            // Check if family document is already open
            var existingFamDoc = docManager.FindOpenFamilyDocument(family);

            if (existingFamDoc != null) {
                Debug.WriteLine($"[OpenAndActivateFamily] Family '{family.Name}' is already open");

                // If it's already the active document, nothing to do
                if (docManager.IsDocActive(existingFamDoc)) {
                    Debug.WriteLine($"[OpenAndActivateFamily] Family doc is active, nothing to do");
                    return;
                }

                // Document is open but not active - need to switch to it
                Debug.WriteLine($"[OpenAndActivateFamily] Family doc is open but not active, switching...");
                ActivateOpenFamilyDocument(uiApp, existingFamDoc, family.Name);
                return;
            }

            // Family document is not open - open it via EditFamily
            Debug.WriteLine($"[OpenAndActivateFamily] Family '{family.Name}' is NOT open, calling EditFamily...");
            var famDoc = docManager.ActiveDoc.EditFamily(family);

            if (famDoc == null) {
                Debug.WriteLine($"[OpenAndActivateFamily] EditFamily returned null for '{family.Name}'");
                return;
            }

            Debug.WriteLine($"[OpenAndActivateFamily] EditFamily returned document '{famDoc.Title}'");
            Debug.WriteLine(docManager.LogDocumentState(context: "After EditFamily"));

            // EditFamily opens the document but does NOT activate it in the UI.
            // ShowElements is unreliable for activation.
            // The reliable approach: save to temp file and use OpenAndActivateDocument
            ActivateOpenFamilyDocument(uiApp, famDoc, family.Name);

        } catch (Exception ex) {
            Debug.WriteLine(docManager.LogDocumentState(context: "OpenAndActivateFamily ERROR"));
            Debug.WriteLine(ex.ToStringDemystified());
        }
    }

    /// <summary>
    /// Activates an already-open family document.
    /// 
    /// IMPORTANT: Family documents opened via EditFamily have a PathName pointing to the original
    /// source file, but Revit won't let you "re-open" that file since it's already loaded.
    /// The reliable solution is to ALWAYS save to a unique temp file and open that.
    /// </summary>
    private static void ActivateOpenFamilyDocument(UIApplication uiApp, Document famDoc, string familyName) {
        var tempPath = SaveFamilyToTempFile(famDoc, familyName);
        Debug.WriteLine($"[ActivateOpenFamilyDocument] Opening from temp path...");
        _ = uiApp.OpenAndActivateDocument(tempPath);
    }

    /// <summary>
    /// Activates an already-open family document and switches to a specific view.
    /// </summary>
    private static void ActivateOpenFamilyDocumentAndView(UIApplication uiApp, Document famDoc, View targetView) {
        var tempPath = SaveFamilyToTempFile(famDoc, famDoc.Title);
        Debug.WriteLine($"[ActivateOpenFamilyDocumentAndView] Opening from temp path...");
        var activatedUiDoc = uiApp.OpenAndActivateDocument(tempPath);

        // Now set the view since we're in the active document
        // Note: The targetView reference may be stale after SaveAs, so find the view by name
        var viewByName = new FilteredElementCollector(activatedUiDoc.Document)
            .OfClass(typeof(View))
            .Cast<View>()
            .FirstOrDefault(v => v.Name == targetView.Name && v.ViewType == targetView.ViewType);

        if (viewByName != null) {
            Debug.WriteLine($"[ActivateOpenFamilyDocumentAndView] Using RequestViewChange to '{viewByName.Name}'");
            activatedUiDoc.RequestViewChange(viewByName);
        } else {
            Debug.WriteLine($"[ActivateOpenFamilyDocumentAndView] Could not find matching view '{targetView.Name}'");
        }
    }

    /// <summary>
    /// Saves a family document to a unique temp file and returns the path.
    /// </summary>
    private static string SaveFamilyToTempFile(Document famDoc, string familyName) {
        // Create a unique temp directory for this session to avoid conflicts
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _ = Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, $"{familyName}.rfa");

        Debug.WriteLine($"[SaveFamilyToTempFile] Saving to temp path: {tempPath}");
        famDoc.SaveAs(tempPath, new SaveAsOptions { OverwriteExistingFile = true });

        return tempPath;
    }

    /// <summary>
    /// Switches to a document and view using ShowElements.
    /// This is the only API-supported way to switch to a non-active document.
    /// 
    /// IMPORTANT: ShowElements requires an actual element visible in a view, NOT the View's ElementId.
    /// We must find an element that exists in the target view and pass that.
    /// </summary>
    private static void SwitchToDocumentView(UIDocument targetUiDoc, View targetView) {
        if (targetView == null) {
            Debug.WriteLine("[SwitchToDocumentView] targetView is null, cannot switch");
            return;
        }

        var doc = targetUiDoc.Document;

        // Find an element visible in the target view to pass to ShowElements
        // Reference Planes are always present in family documents
        var elementToShow = FindElementInView(doc, targetView);

        if (elementToShow == null) {
            Debug.WriteLine($"[SwitchToDocumentView] No showable element found in view '{targetView.Name}'");
            return;
        }

        Debug.WriteLine($"[SwitchToDocumentView] Calling ShowElements with {elementToShow.GetType().Name} '{elementToShow.Name}' (Id: {elementToShow.Id.Value}) in view '{targetView.Name}'");
        targetUiDoc.ShowElements(elementToShow);
    }

    /// <summary>
    /// Finds an element that is visible in the given view, suitable for ShowElements.
    /// Priority: Reference Planes > any element in view.
    /// </summary>
    private static Element FindElementInView(Document doc, View view) {
        // Priority 1: Find Reference Planes (always exist in family documents)
        var referencePlane = new FilteredElementCollector(doc, view.Id)
            .OfClass(typeof(ReferencePlane))
            .FirstOrDefault();

        if (referencePlane != null) {
            Debug.WriteLine($"[FindElementInView] Found ReferencePlane: '{referencePlane.Name}'");
            return referencePlane;
        }

        // Priority 2: Any element in the view that's not an element type
        var anyElement = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .FirstOrDefault();

        if (anyElement != null) {
            Debug.WriteLine($"[FindElementInView] Found element: {anyElement.GetType().Name} (Id: {anyElement.Id.Value})");
            return anyElement;
        }

        Debug.WriteLine($"[FindElementInView] No elements found in view '{view.Name}'");
        return null;
    }

    /// <summary>
    /// Gets the best view to show for a family document.
    /// Priority: 1) MRU view from buffer, 2) "Ref. Level" floor plan, 3) Any valid view
    /// </summary>
    private static View GetBestViewForFamilyDocument(UIDocument famUiDoc) {
        var famDoc = famUiDoc.Document;

        // TODO: Integrate MRU service here when needed
        // For now, just get the default view

        // Non-graphical view types that cannot display content
        var invalidViewTypes = new HashSet<ViewType> {
            ViewType.ProjectBrowser,
            ViewType.SystemBrowser,
            ViewType.Undefined,
            ViewType.Internal,
            ViewType.Report,
            ViewType.CostReport,
            ViewType.LoadsReport,
            ViewType.PresureLossReport,
            ViewType.PanelSchedule,
            ViewType.ColumnSchedule,
            ViewType.Schedule,
        };

        // Get all graphical, non-template views
        var allViews = new FilteredElementCollector(famDoc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate && !invalidViewTypes.Contains(v.ViewType))
            .ToList();

        // Log all available views for debugging
        Debug.WriteLine($"[GetBestViewForFamilyDocument] Available graphical views ({allViews.Count}):");
        foreach (var v in allViews) {
            Debug.WriteLine($"  - '{v.Name}' (Type: {v.ViewType})");
        }

        // Priority 1: "Ref. Level" floor plan (standard in most family templates)
        var refLevelFloorPlan = allViews.FirstOrDefault(v =>
            v.Name == "Ref. Level" && v.ViewType == ViewType.FloorPlan);
        if (refLevelFloorPlan != null) {
            Debug.WriteLine($"[GetBestViewForFamilyDocument] Selected: 'Ref. Level' floor plan");
            return refLevelFloorPlan;
        }

        // Priority 2: Any floor plan (regular 3D families)
        var anyFloorPlan = allViews.FirstOrDefault(v => v.ViewType == ViewType.FloorPlan);
        if (anyFloorPlan != null) {
            Debug.WriteLine($"[GetBestViewForFamilyDocument] Selected: floor plan '{anyFloorPlan.Name}'");
            return anyFloorPlan;
        }

        // Priority 3: Drafting view (common for some annotation families)
        var draftingView = allViews.FirstOrDefault(v => v.ViewType == ViewType.DraftingView);
        if (draftingView != null) {
            Debug.WriteLine($"[GetBestViewForFamilyDocument] Selected: drafting view '{draftingView.Name}'");
            return draftingView;
        }

        // Priority 4: Any elevation view (Front, Back, Left, Right)
        var elevationView = allViews.FirstOrDefault(v => v.ViewType == ViewType.Elevation);
        if (elevationView != null) {
            Debug.WriteLine($"[GetBestViewForFamilyDocument] Selected: elevation '{elevationView.Name}'");
            return elevationView;
        }

        // Priority 5: DrawingSheet - this is correct for annotation/tag families
        // Tag families typically ONLY have Sheet views for displaying their content
        var sheetView = allViews.FirstOrDefault(v => v.ViewType == ViewType.DrawingSheet);
        if (sheetView != null) {
            Debug.WriteLine($"[GetBestViewForFamilyDocument] Selected: sheet '{sheetView.Name}' (typical for annotation families)");
            return sheetView;
        }

        // Fallback: Any remaining graphical view
        var anyView = allViews.FirstOrDefault();
        Debug.WriteLine($"[GetBestViewForFamilyDocument] Fallback to: '{anyView?.Name ?? "null"}' (Type: {anyView?.ViewType})");
        return anyView;
    }
}