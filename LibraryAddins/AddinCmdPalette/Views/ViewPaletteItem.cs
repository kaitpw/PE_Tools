using AddinCmdPalette.Core;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace AddinCmdPalette.Views;

/// <summary>
///     Adapter that wraps Revit View to implement ISelectableItem
/// </summary>
public partial class ViewPaletteItem : ObservableObject, ISelectableItem {
    private readonly View _view;

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double _searchScore;

    public ViewPaletteItem(View view) {
        this._view = view;
    }

    public string PrimaryText => this._view.Name;

    public string SecondaryText {
        get {
            // Get sheet information if view is on a sheet
            var sheetInfo = this.GetSheetInfo();
            return sheetInfo ?? string.Empty;
        }
    }

    public string PillText {
        get {
            // Get View Use parameter (VIEW_DESCRIPTION contains the "View Use" field)
            var viewUseParam = this._view.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION);
            if (viewUseParam != null && !string.IsNullOrEmpty(viewUseParam.AsString())) {
                return viewUseParam.AsString();
            }
            
            return string.Empty;
        }
    }

    private string GetSheetInfo() {
        var doc = this._view.Document;
        
        // Find which sheet this view is on by searching through all sheets
        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>();
        
        foreach (var sheet in sheets) {
            var viewportIds = sheet.GetAllViewports();
            foreach (var viewportId in viewportIds) {
                var viewport = doc.GetElement(viewportId) as Viewport;
                if (viewport?.ViewId == this._view.Id) {
                    var sheetNumber = sheet.SheetNumber;
                    var sheetName = sheet.Name;
                    return $"{sheetNumber} - {sheetName}";
                }
            }
        }
        
        return string.Empty;
    }

    public string TooltipText => $"{this._view.Name}\nType: {this._view.ViewType}\nId: {this._view.Id}";

    public BitmapImage Icon => null;

    /// <summary> Access to underlying view </summary>
    public View View => this._view;
}

