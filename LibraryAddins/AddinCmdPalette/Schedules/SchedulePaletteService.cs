using AddinCmdPalette.Actions;
using AddinCmdPalette.Core;
using AddinCmdPalette.Services;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PeServices.Storage;

namespace AddinCmdPalette.Schedules;

/// <summary>
///     Factory service for creating schedule palette instances
/// </summary>
public static class SchedulePaletteService {
    /// <summary>
    ///     Creates a schedule palette window configured for opening Revit schedules
    /// </summary>
    public static SelectablePalette Create(
        UIApplication uiApp,
        Storage persistence
    ) {
        var doc = uiApp.ActiveUIDocument.Document;
        // Get all schedule views (ViewSchedule is a subclass of View)
        // Exclude templates and revision schedules
        var schedules = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Where(s => !s.IsTemplate && !s.Name.Contains("<Revision Schedule>"))
            .OrderBy(s => s.Name)
            .ToList();

        // Convert to ISelectableItem adapters
        var selectableItems = schedules
            .Select(schedule => new SchedulePaletteItem(schedule))
            .Cast<ISelectableItem>()
            .ToList();

        // Create search filter service
        var searchService = new SearchFilterService(
            persistence,
            item => {
                if (item is SchedulePaletteItem scheduleItem)
                    return scheduleItem.Schedule.Id.ToString();
                return item.PrimaryText;
            },
            "SchedulePalette"
        );

        // Create actions
        var actions = new List<PaletteAction> {
            new() {
                Name = "Open Schedule",
                ExecuteAsync = async item => {
                    if (item is SchedulePaletteItem scheduleItem) {
                        try {
                            // Must run on main thread, not Task.Run
                            // ViewSchedule inherits from View, so setting ActiveView works
                            uiApp.ActiveUIDocument.ActiveView = scheduleItem.Schedule;
                        } catch (Autodesk.Revit.Exceptions.InvalidOperationException ex) {
                            throw new InvalidOperationException(
                                $"Cannot open schedule '{scheduleItem.Schedule.Name}'. The document may be read-only or the schedule cannot be activated.",
                                ex
                            );
                        } catch (Exception ex) {
                            throw new InvalidOperationException(
                                $"Failed to open schedule '{scheduleItem.Schedule.Name}': {ex.Message}",
                                ex
                            );
                        }
                    }
                },
                CanExecute = item => {
                    if (item is SchedulePaletteItem scheduleItem) {
                        var schedule = scheduleItem.Schedule;
                        // Schedules can be opened if they're not templates
                        // Check if the view can be activated (schedules are views)
                        return !schedule.IsTemplate;
                    }
                    return false;
                }
            }
        };

        // Create view model
        var viewModel = new SelectablePaletteViewModel(selectableItems, searchService);

        // Create and return palette window
        var palette = new SelectablePalette(viewModel, actions);
        palette.Title = "Open Schedule";
        return palette;
    }
}

