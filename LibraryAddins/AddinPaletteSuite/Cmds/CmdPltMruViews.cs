using AddinPaletteSuite.Core;
using AddinPaletteSuite.Core.Services;
using AddinPaletteSuite.Core.Ui;
using PeServices.Storage;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Theme = AddinPaletteSuite.Core.Ui.ThemeManager;
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
                    Execute = item => {
                        var targetDoc = item.View.Document;
                        var currentDoc = uiapp.ActiveUIDocument?.Document;

                        if (currentDoc != null && targetDoc.Equals(currentDoc)) {
                            // Already on the correct document, just set the view
                            uiapp.ActiveUIDocument.ActiveView = item.View;
                        } else {
                            // Switch to the target document first by using OpenAndActivateDocument
                            // This works for already open documents and brings them to the front
                            var docPath = !string.IsNullOrEmpty(targetDoc.PathName)
                                ? targetDoc.PathName
                                : targetDoc.Title;

                            // OpenAndActivateDocument activates the document if already open
                            var activatedUIDoc = uiapp.OpenAndActivateDocument(docPath);

                            // Now set the view
                            activatedUIDoc.ActiveView = item.View;
                        }
                    },
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
            var viewModel = new SelectablePaletteViewModel<MruViewPaletteItem>(selectableItems, searchService);

            // Select second item (index 1) instead of first for MRU behavior
            if (viewModel.FilteredItems.Count > 1) viewModel.SelectedIndex = 1;

            // Create custom key bindings for MRU navigation
            var customKeys = new CustomKeyBindings();
            customKeys.Add(Key.OemTilde, NavigationAction.MoveDown, ModifierKeys.Control); // Ctrl+` cycles forward
            customKeys.Add(Key.OemTilde, NavigationAction.MoveUp,
                ModifierKeys.Control | ModifierKeys.Shift); // Ctrl+Shift+` cycles backward

            // Create palette UserControl with custom key bindings
            var palette = new SelectablePalette<MruViewPaletteItem>(viewModel, actions, customKeys);

            // Hide search box for MRU views (we only navigate with keyboard)
            palette.HideSearchBox();

            // Callback to execute selected view when Ctrl is released
            void OnCtrlReleased() {
                try {
                    var selectedItem = viewModel.SelectedItem;
                    if (selectedItem?.View == null) return;
                    var targetDoc = selectedItem.View.Document;
                    var currentDoc = uiapp.ActiveUIDocument?.Document;

                    if (currentDoc != null && targetDoc.Equals(currentDoc))
                        uiapp.ActiveUIDocument.ActiveView = selectedItem.View;
                    else {
                        var docPath = !string.IsNullOrEmpty(targetDoc.PathName)
                            ? targetDoc.PathName
                            : targetDoc.Title;
                        var activatedUiDoc = uiapp.OpenAndActivateDocument(docPath);
                        activatedUiDoc.ActiveView = selectedItem.View;
                    }

                    EphemeralWindow.RestoreRevitFocus();
                } catch {
                }
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
}

/// <summary>
///     Adapter that wraps Revit View to implement IPaletteListItem for MRU views
/// </summary>
public class MruViewPaletteItem : IPaletteListItem {
    public MruViewPaletteItem(View view) {
        this.View = view;
        var color = DocumentColorService.Instance.GetOrCreateDocumentColor(view.Document);
        this.ItemColor = color;
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