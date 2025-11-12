using AddinCmdPalette.Actions;
using AddinCmdPalette.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using Nice3point.Revit.Extensions;
using System.Windows.Media.Imaging;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdOpenView : BaseCmdPalette {
    public override string TypeName => "view";

    public override IEnumerable<ISelectableItem> GetItems(Document doc) =>
        // Get all views (excluding view templates, legends, sheets, schedules, drafting views, and groups)
        new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate
                        && v.ViewType != ViewType.Legend
                        && v.ViewType != ViewType.DrawingSheet
                        && v.ViewType != ViewType.DraftingView
                        && v.ViewType != ViewType.SystemBrowser
                        && v is not ViewSchedule)
            .OrderBy(v => v.Name)
            .ToList()
            .Select(view => new ViewPaletteItem(view));

    public override string GetPersistenceKey(ISelectableItem item) {
        if (item is ViewPaletteItem viewItem)
            return viewItem.View.Id.ToString();
        return item.PrimaryText;
    }

    public override IEnumerable<PaletteAction> GetActions(UIApplication uiApp) =>
        new List<PaletteAction> {
            new() {
                Name = "Open View",
                ExecuteAsync = async item => {
                    if (item is ViewPaletteItem viewItem) {
                        try {
                            // Must run on main thread, not Task.Run
                            uiApp.ActiveUIDocument.ActiveView = viewItem.View;
                        } catch (Exception ex) {
                            throw new InvalidOperationException(
                                $"Failed to open view '{viewItem.View.Name}': {ex.Message}");
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
public partial class ViewPaletteItem(View view) : ObservableObject, ISelectableItem {
    private readonly string _discipline = view.HasViewDiscipline()
        ? view.Discipline.ToString()
        : string.Empty;

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double _searchScore;

    // Use HasViewDiscipline to check before accessing to avoid exceptions

    public View View { get; } = view;
    public string PrimaryText => this.View.Name;
    public string SecondaryText => this.GetSheetInfo() == null ? $"Sheeted on: {this.GetSheetInfo()}" : "Not Sheeted";
    public string PillText => this.View.FindParameter("View Use")?.AsString() ?? string.Empty;

    public string TooltipText =>
        $"Assoc. Lvl:{this.View.FindParameter(BuiltInParameter.PLAN_VIEW_LEVEL)?.AsValueString()}" +
        $"\nDetail Lvl: {this.View.DetailLevel}" +
        $"\nDiscipline: {this._discipline}" +
        $"\nType: {this.View.ViewType}" +
        $"\nId: {this.View.Id}";

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