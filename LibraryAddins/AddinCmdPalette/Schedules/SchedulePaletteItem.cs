using AddinCmdPalette.Core;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;

namespace AddinCmdPalette.Schedules;

/// <summary>
///     Adapter that wraps Revit ViewSchedule to implement ISelectableItem
/// </summary>
public partial class SchedulePaletteItem : ObservableObject, ISelectableItem {
    private readonly ViewSchedule _schedule;

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double _searchScore;

    public SchedulePaletteItem(ViewSchedule schedule) {
        this._schedule = schedule;
    }

    public string PrimaryText => this._schedule.Name;

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
            if (this._schedule.Document.GetElement(this._schedule.GetTypeId()) is ViewFamilyType vft) {
                return vft.ViewFamily.ToString();
            }
            return string.Empty;
        }
    }

    private string GetSheetInfo() {
        var doc = this._schedule.Document;

        // Find which sheet this schedule is on by searching through all sheets
        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>();

        foreach (var sheet in sheets) {
            var viewportIds = sheet.GetAllViewports();
            foreach (var viewportId in viewportIds) {
                var viewport = doc.GetElement(viewportId) as Viewport;
                if (viewport?.ViewId == this._schedule.Id) {
                    var sheetNumber = sheet.SheetNumber;
                    var sheetName = sheet.Name;
                    return $"{sheetNumber} - {sheetName}";
                }
            }
        }

        return string.Empty;
    }

    public string TooltipText => $"{this._schedule.Name}\nType: Schedule\nId: {this._schedule.Id}";

    public BitmapImage Icon => null;

    /// <summary> Access to underlying schedule view </summary>
    public ViewSchedule Schedule => this._schedule;
}

