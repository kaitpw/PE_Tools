using AddinPaletteSuite.Core.Actions;
using AddinPaletteSuite.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using Nice3point.Revit.Extensions;
using System.Windows.Media.Imaging;
using AddinPaletteSuite.Core.Ui;
namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdOpenSchedule : BaseCmdPalette {
    public override string TypeName => "Schedule";

    public override IEnumerable<IPaletteListItem> GetItems(Document doc) =>
        // Get all schedule views (ViewSchedule is a subclass of View). Exclude templates and revision schedules
        new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Where(s => !s.Name.Contains("<Revision Schedule>"))
            .OrderBy(s => s.Name)
            .ToList()
            .Select(schedule => new SchedulePaletteItem(schedule));

    public override string GetPersistenceKey(IPaletteListItem item) {
        if (item is SchedulePaletteItem scheduleItem)
            return scheduleItem.Schedule.Id.ToString();
        return item.PrimaryText;
    }

    public override IEnumerable<PaletteAction> GetActions(UIApplication uiApp) =>
        new List<PaletteAction> {
            new() {
                Name = "Open Schedule",
                ExecuteAsync = async item => {
                    if (item is SchedulePaletteItem scheduleItem) {
                        try {
                            uiApp.ActiveUIDocument.ActiveView = scheduleItem.Schedule;
                        } catch (Exception ex) {
                            throw new InvalidOperationException(
                                $"Failed to open schedule '{scheduleItem.Schedule.Name}': {ex.Message}"
                            );
                        }
                    }
                },
                CanExecute = item => item is SchedulePaletteItem
            }
        };
}

/// <summary>
///     Adapter that wraps Revit ViewSchedule to implement ISelectableItem
/// </summary>
public partial class SchedulePaletteItem(ViewSchedule schedule) : ObservableObject, IPaletteListItem {
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double _searchScore;
    public ViewSchedule Schedule { get; } = schedule;
    public string PrimaryText => this.Schedule.Name;

    public string SecondaryText {
        get {
            var sheets = this.GetSheetInfo();
            if (sheets.Count == 0) return string.Empty;
            var nums = sheets.Select(s => s.num).Where(n => !string.IsNullOrEmpty(n));
            return $"Sheeted on ({sheets.Count}): {string.Join(", ", nums)}";
        }
    }

    public string PillText { get; } = schedule.FindParameter("Discipline")?.AsValueString();

    public string TooltipText {
        get {
            var sheets = this.GetSheetInfo();
            var sheetText = sheets.Count == 0
                ? "None"
                : string.Join("\n  ", sheets.Select(s => $"{s.num} - {s.name}"));
            return $"Id: {this.Schedule.Id}" +
                   $"\nDiscipline: {this.PillText}" +
                   $"\nSheeted on:\n\t{sheetText}";
        }
    }

    public BitmapImage Icon => null;

    private List<(string num, string name)> GetSheetInfo() {
        var sheetInfo = new List<(string num, string name)>();
        foreach (var inst in this.Schedule.GetScheduleInstances(-1)) {
            var doc = this.Schedule.Document;
            var ownerViewId = doc.GetElement(inst).OwnerViewId;
            var ownerView = doc.GetElement(ownerViewId);
            if (ownerView is ViewSheet view) {
                var num = view.FindParameter(BuiltInParameter.SHEET_NUMBER)?.AsValueString() ?? string.Empty;
                var name = view.FindParameter(BuiltInParameter.SHEET_NAME)?.AsValueString() ?? string.Empty;
                sheetInfo.Add((num, name));
            }
        }

        return sheetInfo;
    }
}