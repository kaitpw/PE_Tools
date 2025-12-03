using AddinPaletteSuite.Core.Services;
using PeServices.Storage;
using PeUi.Components;
using PeUi.Core;
using PeUi.Core.Services;
using PeUi.ViewModels;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WpfColor = System.Windows.Media.Color;

namespace AddinPaletteSuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdPltMruViews : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        try {
            var uiapp = commandData.Application;
            // Get MRU ordered views from all open documents
            var mruViews = MruViewService.Instance.GetMruOrderedViews(uiapp);
            var selectableItems = mruViews.Select(v => new MruViewPaletteItem(v)).ToList();

            // Create actions - single action to open view
            var actions = new List<PaletteAction<MruViewPaletteItem>> {
                new() {
                    Name = "Open View",
                    Execute = item => ActivateView(uiapp, item.View),
                    CanExecute = item => item?.View != null && item.View.CanBePrinted
                }
            };

            // Create minimal search service without persistence (MRU ordering is handled by service)
            var persistence = new Storage(nameof(CmdPltMruViews));
            var searchService = new SearchFilterService<MruViewPaletteItem>(
                persistence,
                item => item.View.Id.ToString(),
                enableUsageTracking: false);

            // Create view model
            var viewModel = new PaletteViewModel<MruViewPaletteItem>(selectableItems, searchService);

            // Select second item (index 1) instead of first for MRU behavior
            if (viewModel.FilteredItems.Count > 1) viewModel.SelectedIndex = 1;

            // Create custom key bindings for MRU navigation
            var customKeys = new CustomKeyBindings();
            customKeys.Add(Key.OemTilde, NavigationAction.MoveDown, ModifierKeys.Control); // Ctrl+` cycles forward
            customKeys.Add(Key.OemTilde, NavigationAction.MoveUp,
                ModifierKeys.Control | ModifierKeys.Shift); // Ctrl+Shift+` cycles backward

            // Create palette using composition pattern (NOT inheritance)
            // Generic classes cannot inherit from XAML partial classes in Revit-hosted WPF
            var palette = new Palette();
            palette.Initialize(viewModel, actions, customKeys);

            // Hide search box for MRU views (we only navigate with keyboard)
            palette.HideSearchBox();

            // Callback to execute selected view when Ctrl is released
            void OnCtrlReleased() {
                var selectedItem = viewModel.SelectedItem;
                if (selectedItem?.View != null)
                    ActivateView(uiapp, selectedItem.View);
            }

            // Wrap in EphemeralWindow with Ctrl key monitoring and show
            var window = new EphemeralWindow(palette, "MRU Views", true, OnCtrlReleased);
            window.Show();

            return Result.Succeeded;
        } catch (Exception ex) {
            message = $"Error opening MRU views palette: {ex.Message}";
            return Result.Failed;
        }
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