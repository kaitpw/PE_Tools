using AddinCmdPalette.Actions;
using AddinCmdPalette.Core;
using AddinCmdPalette.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using PeServices.Storage;
using System.Windows.Media.Imaging;
using InvalidOperationException = Autodesk.Revit.Exceptions.InvalidOperationException;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdOpenView : CmdOpenPaletteBase {
    protected override string PaletteTypeName => "view";
    protected override string PaletteTitle => "Open View";

    protected override IEnumerable<ISelectableItem> GetSelectableItems(UIApplication uiApp) {
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
        return views
            .Select(view => new ViewPaletteItem(view));
    }

    protected override SearchFilterService GetSearchFilterService(Storage persistence,
        IEnumerable<ISelectableItem> items) =>
        new(
            persistence,
            item => {
                if (item is ViewPaletteItem viewItem)
                    return viewItem.View.Id.ToString();
                return item.PrimaryText;
            },
            "ViewPalette"
        );

    protected override IEnumerable<PaletteAction> GetActions(UIApplication uiApp) =>
        new List<PaletteAction> {
            new() {
                Name = "Open View",
                ExecuteAsync = async item => {
                    if (item is ViewPaletteItem viewItem) {
                        try {
                            // Must run on main thread, not Task.Run
                            uiApp.ActiveUIDocument.ActiveView = viewItem.View;
                        } catch (InvalidOperationException ex) {
                            throw new System.InvalidOperationException(
                                $"Cannot open view '{viewItem.View.Name}'. The document may be read-only or the view cannot be activated.",
                                ex
                            );
                        } catch (Exception ex) {
                            throw new System.InvalidOperationException(
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
}

/// <summary>
///     Adapter that wraps Revit View to implement ISelectableItem
/// </summary>
public partial class ViewPaletteItem : ObservableObject, ISelectableItem {
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double _searchScore;

    public ViewPaletteItem(View view) => this.View = view;

    /// <summary> Access to underlying view </summary>
    public View View { get; }

    public string PrimaryText => this.View.Name;

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
            var viewUseParam = this.View.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION);
            if (viewUseParam != null && !string.IsNullOrEmpty(viewUseParam.AsString())) return viewUseParam.AsString();

            return string.Empty;
        }
    }

    public string TooltipText => $"{this.View.Name}\nType: {this.View.ViewType}\nId: {this.View.Id}";

    public BitmapImage Icon => null;

    private string GetSheetInfo() {
        var doc = this.View.Document;

        // Find which sheet this view is on by searching through all sheets
        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>();

        foreach (var sheet in sheets) {
            var viewportIds = sheet.GetAllViewports();
            foreach (var viewportId in viewportIds) {
                var viewport = doc.GetElement(viewportId) as Viewport;
                if (viewport?.ViewId == this.View.Id) {
                    var sheetNumber = sheet.SheetNumber;
                    var sheetName = sheet.Name;
                    return $"{sheetNumber} - {sheetName}";
                }
            }
        }

        return string.Empty;
    }
}