using AddinCmdPalette.Actions;
using AddinCmdPalette.Core;
using AddinCmdPalette.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using PeServices.Storage;
using System.Windows.Media.Imaging;
using InvalidOperationException = Autodesk.Revit.Exceptions.InvalidOperationException;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdOpenSchedule : CmdOpenPaletteBase {
    protected override string PaletteTypeName => "schedule";
    protected override string PaletteTitle => "Open Schedule";

    protected override IEnumerable<ISelectableItem> GetSelectableItems(UIApplication uiApp) {
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
        return schedules
            .Select(schedule => new SchedulePaletteItem(schedule));
    }

    protected override SearchFilterService GetSearchFilterService(Storage persistence,
        IEnumerable<ISelectableItem> items) =>
        new(
            persistence,
            item => {
                if (item is SchedulePaletteItem scheduleItem)
                    return scheduleItem.Schedule.Id.ToString();
                return item.PrimaryText;
            },
            "SchedulePalette"
        );

    protected override IEnumerable<PaletteAction> GetActions(UIApplication uiApp) =>
        new List<PaletteAction> {
            new() {
                Name = "Open Schedule",
                ExecuteAsync = async item => {
                    if (item is SchedulePaletteItem scheduleItem) {
                        try {
                            // Must run on main thread, not Task.Run
                            // ViewSchedule inherits from View, so setting ActiveView works
                            uiApp.ActiveUIDocument.ActiveView = scheduleItem.Schedule;
                        } catch (InvalidOperationException ex) {
                            throw new System.InvalidOperationException(
                                $"Cannot open schedule '{scheduleItem.Schedule.Name}'. The document may be read-only or the schedule cannot be activated.",
                                ex
                            );
                        } catch (Exception ex) {
                            throw new System.InvalidOperationException(
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
}

/// <summary>
///     Adapter that wraps Revit ViewSchedule to implement ISelectableItem
/// </summary>
public partial class SchedulePaletteItem : ObservableObject, ISelectableItem {
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double _searchScore;

    public SchedulePaletteItem(ViewSchedule schedule) => this.Schedule = schedule;

    /// <summary> Access to underlying schedule view </summary>
    public ViewSchedule Schedule { get; }

    public string PrimaryText => this.Schedule.Name;

    public string SecondaryText {
        get {
            // Get sheet information if schedule is on a sheet
            var sheetInfo = this.GetSheetInfo();
            return sheetInfo ?? string.Empty;
        }
    }

    public string PillText {
        get {
            // Get discipline from ViewFamilyType
            if (this.Schedule.Document.GetElement(this.Schedule.GetTypeId()) is ViewFamilyType vft)
                return vft.ViewFamily.ToString();
            return string.Empty;
        }
    }

    public string TooltipText => $"{this.Schedule.Name}\nType: Schedule\nId: {this.Schedule.Id}";

    public BitmapImage Icon => null;

    private string GetSheetInfo() {
        var doc = this.Schedule.Document;

        // Find which sheet this schedule is on by searching through all sheets
        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>();

        foreach (var sheet in sheets) {
            var viewportIds = sheet.GetAllViewports();
            foreach (var viewportId in viewportIds) {
                var viewport = doc.GetElement(viewportId) as Viewport;
                if (viewport?.ViewId == this.Schedule.Id) {
                    var sheetNumber = sheet.SheetNumber;
                    var sheetName = sheet.Name;
                    return $"{sheetNumber} - {sheetName}";
                }
            }
        }

        return string.Empty;
    }
}