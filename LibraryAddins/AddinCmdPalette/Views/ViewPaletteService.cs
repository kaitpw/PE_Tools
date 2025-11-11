using AddinCmdPalette.Actions;
using AddinCmdPalette.Core;
using AddinCmdPalette.Services;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PeServices.Storage;

namespace AddinCmdPalette.Views;

/// <summary>
///     Factory service for creating view palette instances
/// </summary>
public static class ViewPaletteService {
    /// <summary>
    ///     Creates a view palette window configured for opening Revit views
    /// </summary>
    public static SelectablePalette Create(
        UIApplication uiApp,
        Storage persistence
    ) {
        var doc = uiApp.ActiveUIDocument.Document;

        // Get all views (excluding templates, legends, sheets, schedules, drafting views, and groups)
        var views = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate
                && v.ViewType != ViewType.Legend
                && v.ViewType != ViewType.DrawingSheet
                && v.ViewType != ViewType.DraftingView
                && v is not ViewSchedule)
            .OrderBy(v => v.Name)
            .ToList();

        // Convert to ISelectableItem adapters
        var selectableItems = views
            .Select(view => new ViewPaletteItem(view))
            .Cast<ISelectableItem>()
            .ToList();

        // Create search filter service
        var searchService = new SearchFilterService(
            persistence,
            item => {
                if (item is ViewPaletteItem viewItem)
                    return viewItem.View.Id.ToString();
                return item.PrimaryText;
            },
            "ViewPalette"
        );

        // Create actions
        var actions = new List<PaletteAction> {
            new() {
                Name = "Open View",
                ExecuteAsync = async item => {
                    if (item is ViewPaletteItem viewItem) {
                        try {
                            // Must run on main thread, not Task.Run
                            uiApp.ActiveUIDocument.ActiveView = viewItem.View;
                        } catch (Autodesk.Revit.Exceptions.InvalidOperationException ex) {
                            throw new InvalidOperationException(
                                $"Cannot open view '{viewItem.View.Name}'. The document may be read-only or the view cannot be activated.",
                                ex
                            );
                        } catch (Exception ex) {
                            throw new InvalidOperationException(
                                $"Failed to open view '{viewItem.View.Name}': {ex.Message}",
                                ex
                            );
                        }
                    }
                },
                CanExecute = item => {
                    if (item is ViewPaletteItem viewItem) {
                        // Check if view can be opened
                        return viewItem.View.CanBePrinted;
                    }
                    return false;
                }
            }
        };

        // Create view model
        var viewModel = new SelectablePaletteViewModel(selectableItems, searchService);

        // Create and return palette window
        var palette = new SelectablePalette(viewModel, actions);
        palette.Title = "Open View";
        return palette;
    }
}

