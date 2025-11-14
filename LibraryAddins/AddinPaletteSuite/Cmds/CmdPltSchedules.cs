using AddinPaletteSuite.Core.Actions;
using AddinPaletteSuite.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using Nice3point.Revit.Extensions;
using System.Windows.Media.Imaging;
using AddinPaletteSuite.Core.Ui;
namespace AddinPaletteSuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdPltSchedules : BaseCmdPalette<ViewSchedule, SchedulePaletteItem> {
    public override string TypeName => "Schedule";
    public override IEnumerable<SchedulePaletteItem> GetItems(IEnumerable<ViewSchedule> schedules, Document doc) =>
        schedules.Where(s => !s.Name.Contains("<Revision Schedule>"))
            .Select(schedule => new SchedulePaletteItem(schedule));

    public override string GetPersistenceKey(SchedulePaletteItem item) => item.Schedule.Id.ToString();

    public override IEnumerable<PaletteAction<SchedulePaletteItem>> GetActions(UIApplication uiApp) =>
        new List<PaletteAction<SchedulePaletteItem>> {
            new() {
                Name = "Open",
                Execute = item => uiApp.ActiveUIDocument.ActiveView = item.Schedule,
                CanExecute = item => item != null && item.Schedule.CanBePrinted
            }
        };
}

/// <summary>
///     Adapter that wraps Revit ViewSchedule to implement ISelectableItem
/// </summary>
public partial class SchedulePaletteItem(ViewSchedule schedule) : BaseObservableListItem, IPaletteListItem {
    public ViewSchedule Schedule { get; } = schedule;
    public string TextPrimary => this.Schedule.Name;

    public string TextSecondary {
        get {
            var sheets = this.GetSheetInfo();
            if (sheets.Count == 0) return string.Empty;
            var nums = sheets.Select(s => s.num).Where(n => !string.IsNullOrEmpty(n));
            return $"Sheeted on ({sheets.Count}): {string.Join(", ", nums)}";
        }
    }

    public string TextPill { get; } = schedule.FindParameter("Discipline")?.AsValueString();

    public string TextInfo {
        get {
            var sheets = this.GetSheetInfo();
            var sheetText = sheets.Count == 0
                ? "None"
                : string.Join("\n  ", sheets.Select(s => $"{s.num} - {s.name}"));
            return $"Id: {this.Schedule.Id}" +
                   $"\nDiscipline: {this.TextPill}" +
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