using AddinPaletteSuite.Core.Services;
using PeRevit.Ui;
using PeUi.Core;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WpfColor = System.Windows.Media.Color;

namespace AddinPaletteSuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdPltMruViews : IExternalCommand {
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elementSet) {
        try {
            var uiapp = commandData.Application;
            Open(uiapp);
            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Failed;
        }
    }

    public static void Open(UIApplication uiapp) {
        var items = MruViewService.Instance
            .GetMruOrderedViews(uiapp)
            .Select(v => new MruViewPaletteItem(v));

        var customKeys = new CustomKeyBindings();
        customKeys.Add(Key.OemTilde, NavigationAction.MoveDown, ModifierKeys.Control); // Ctrl+` cycles forward
        customKeys.Add(Key.OemTilde, NavigationAction.MoveUp, ModifierKeys.Control | ModifierKeys.Shift); // Ctrl+Shift+` cycles backward

        var window = PaletteFactory.Create("Mru Views Palette", items, new List<PaletteAction<MruViewPaletteItem>>(),
            new PaletteOptions<MruViewPaletteItem> {
                SearchConfig = null, // Disable search for MRU palette
                CustomKeyBindings = customKeys,
                ViewModelMutator = vm => {
                    // Select second item (first is current view, second is previous)
                    if (vm.FilteredItems.Count > 1) vm.SelectedIndex = 1;
                },
                OnCtrlReleased = vm => {
                    var selectedItem = vm.SelectedItem;
                    if (selectedItem?.View != null)
                        return () => ActivateView(uiapp, selectedItem.View);
                    return null;
                }
            });
        window.Show();
    }

    /// <summary>
    ///     Activates a view, switching documents if necessary.
    ///     Supports local, cloud-hosted (Autodesk Docs), and family documents.
    /// </summary>
    private static void ActivateView(UIApplication uiapp, View targetView) {
        var targetDoc = targetView.Document;
        var currentDoc = uiapp.ActiveUIDocument?.Document;

        Debug.WriteLine($"[MruViews] ActivateView: targetView='{targetView.Name}', targetDoc='{targetDoc.Title}'");
        Debug.WriteLine($"[MruViews] ActivateView: currentDoc='{currentDoc?.Title ?? "null"}'");
        Debug.WriteLine($"[MruViews] ActivateView: targetDoc.PathName='{targetDoc.PathName}', IsModelInCloud={targetDoc.IsModelInCloud}");

        // Same document - just switch views
        if (currentDoc != null && targetDoc.Equals(currentDoc)) {
            Debug.WriteLine("[MruViews] Same document, switching view directly");
            uiapp.ActiveUIDocument.ActiveView = targetView;
            return;
        }

        // Different document - try multiple approaches
        // Approach 1: For documents with a valid path, use OpenAndActivateDocument
        ModelPath modelPath = null;
        if (targetDoc.IsModelInCloud) {
            modelPath = targetDoc.GetCloudModelPath();
            Debug.WriteLine("[MruViews] Cloud document, using GetCloudModelPath()");
        } else if (!string.IsNullOrEmpty(targetDoc.PathName)) {
            modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(targetDoc.PathName);
            Debug.WriteLine("[MruViews] Local document, converted PathName to ModelPath");
        }

        if (modelPath != null) {
            Debug.WriteLine("[MruViews] Opening and activating document via ModelPath");
            var openOptions = new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DoNotDetach };
            var activatedUiDoc = uiapp.OpenAndActivateDocument(modelPath, openOptions, false);
            activatedUiDoc.ActiveView = targetView;
            Debug.WriteLine("[MruViews] Document activated successfully via ModelPath");
            return;
        }

        // Approach 2: For unsaved/pathless documents, use ShowElements workaround.
        // Per Building Coder: "When you call UIDocument.ShowElements, the active Document 
        // will change to the document you are showing elements in"
        // https://thebuildingcoder.typepad.com/blog/2018/04/switch-view-or-document-by-showing-elements.html
        Debug.WriteLine("[MruViews] No ModelPath available, trying ShowElements workaround...");
        Debug.WriteLine($"[MruViews] targetDoc.Title='{targetDoc.Title}', IsFamilyDocument={targetDoc.IsFamilyDocument}");

        try {
            var targetUiDoc = new UIDocument(targetDoc);

            // Find any element in the target view to use with ShowElements
            // This will activate the document as a side effect
            var elementInView = new FilteredElementCollector(targetDoc, targetView.Id)
                .WhereElementIsNotElementType()
                .FirstElementId();

            if (elementInView != null && elementInView != ElementId.InvalidElementId) {
                Debug.WriteLine($"[MruViews] Found element {elementInView} in view, calling ShowElements...");
                targetUiDoc.ShowElements(elementInView);
            } else {
                // Fallback: try showing the view element itself
                Debug.WriteLine("[MruViews] No elements in view, trying to show view element itself...");
                targetUiDoc.ShowElements(targetView.Id);
            }

            // After ShowElements activates the document, set the view we actually want
            // (ShowElements may have opened a different view to show the element)
            Debug.WriteLine("[MruViews] Setting ActiveView after ShowElements...");
            targetUiDoc.ActiveView = targetView;
            Debug.WriteLine("[MruViews] Document activated successfully via ShowElements workaround");
        } catch (Exception ex) {
            Debug.WriteLine($"[MruViews] ShowElements workaround failed: {ex.Message}");
            throw new InvalidOperationException(
                $"Cannot switch to document '{targetDoc.Title}'. Error: {ex.Message}", ex);
        }
    }
}

/// <summary>
///     Adapter that wraps Revit View to implement IPaletteListItem for MRU views
/// </summary>
public class MruViewPaletteItem : IPaletteListItem {
    public MruViewPaletteItem(View view) {
        this.View = view;
        Debug.WriteLine($"[MruViewPaletteItem] Creating item for view '{view.Name}' in doc '{view.Document.Title}'");
        var color = DocumentColorService.Instance.GetOrCreateDocumentColor(view.Document);
        this.ItemColor = color;
        Debug.WriteLine($"[MruViewPaletteItem] Item created with color #{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    public View View { get; }
    public string TextPrimary => this.View.Name;
    public string TextSecondary => this.View.Document.Title;
    public string TextPill => this.View.ViewType.ToString();

    public string TextInfo =>
        $"Document: {this.View.Document.Title}\nView Type: {this.View.ViewType}\nId: {this.View.Id}";

    public BitmapImage Icon => null;
    public WpfColor? ItemColor { get; }
}
