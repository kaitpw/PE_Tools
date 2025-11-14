using AddinPaletteSuite.Core.Actions;
using AddinPaletteSuite.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using Nice3point.Revit.Extensions;
using System.Windows.Media.Imaging;
using AddinPaletteSuite.Core.Ui;
namespace AddinPaletteSuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdPltViews : BaseCmdPalette<View, ViewPaletteItem> {
    public override string TypeName => "view";

    public override IEnumerable<ViewPaletteItem> GetItems(IEnumerable<View> views, Document doc) =>
        views.Where(v => !v.IsTemplate
                        && v.ViewType != ViewType.Legend
                        && v.ViewType != ViewType.DrawingSheet
                        && v.ViewType != ViewType.DraftingView
                        && v.ViewType != ViewType.SystemBrowser
                        && v is not ViewSchedule)
            .Select(view => new ViewPaletteItem(view));

    public override string GetPersistenceKey(ViewPaletteItem item) => item.View.Id.ToString();

    public override IEnumerable<PaletteAction<ViewPaletteItem>> GetActions(UIApplication uiApp) =>
        new List<PaletteAction<ViewPaletteItem>> {
            new() {
                Name = "Open View",
                Execute = item => uiApp.ActiveUIDocument.ActiveView = item.View,
                CanExecute = item => item != null && item.View.CanBePrinted
            }
        };
}

/// <summary>
///     Adapter that wraps Revit View to implement ISelectableItem
/// </summary>
public partial class ViewPaletteItem(View view) : BaseObservableListItem, IPaletteListItem {
    private readonly string _discipline = view.HasViewDiscipline()
        ? view.Discipline.ToString()
        : string.Empty;

    // Use HasViewDiscipline to check before accessing to avoid exceptions

    public View View { get; } = view;
    public string TextPrimary => this.View.Name;
    public string TextSecondary => this.GetSheetInfo() == null ? $"Sheeted on: {this.GetSheetInfo()}" : "Not Sheeted";
    public string TextPill => this.View.FindParameter("View Use")?.AsString() ?? string.Empty;

    public string TextInfo =>
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