using AddinCmdPalette.Actions;
using AddinCmdPalette.Core;
using AddinCmdPalette.Services;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PeServices.Storage;

namespace AddinCmdPalette.Sheets;

/// <summary>
///     Factory service for creating sheet palette instances
/// </summary>
public static class SheetPaletteService {
    /// <summary>
    ///     Creates a sheet palette window configured for opening Revit sheets
    /// </summary>
    public static SelectablePalette Create(
        UIApplication uiApp,
        Storage persistence
    ) {
        var doc = uiApp.ActiveUIDocument.Document;

        // Get all sheet views
        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .OrderBy(s => s.SheetNumber)
            .ToList();

        // Convert to ISelectableItem adapters
        var selectableItems = sheets
            .Select(sheet => new SheetPaletteItem(sheet))
            .Cast<ISelectableItem>()
            .ToList();

        // Create search filter service
        var searchService = new SearchFilterService(
            persistence,
            item => {
                if (item is SheetPaletteItem sheetItem)
                    return sheetItem.Sheet.Id.ToString();
                return item.PrimaryText;
            },
            "SheetPalette"
        );

        // Create actions
        var actions = new List<PaletteAction> {
            new() {
                Name = "Open Sheet",
                ExecuteAsync = async item => {
                    if (item is SheetPaletteItem sheetItem) {
                        try {
                            // Must run on main thread, not Task.Run
                            // ViewSheet inherits from View, so setting ActiveView works
                            uiApp.ActiveUIDocument.ActiveView = sheetItem.Sheet;
                        } catch (Autodesk.Revit.Exceptions.InvalidOperationException ex) {
                            throw new InvalidOperationException(
                                $"Cannot open sheet '{sheetItem.Sheet.Name}'. The document may be read-only or the sheet cannot be activated.",
                                ex
                            );
                        } catch (Exception ex) {
                            throw new InvalidOperationException(
                                $"Failed to open sheet '{sheetItem.Sheet.Name}': {ex.Message}",
                                ex
                            );
                        }
                    }
                },
                CanExecute = item => item is SheetPaletteItem
            }
        };

        // Create view model
        var viewModel = new SelectablePaletteViewModel(selectableItems, searchService);

        // Create and return palette window
        var palette = new SelectablePalette(viewModel, actions);
        palette.Title = "Open Sheet";
        return palette;
    }
}

