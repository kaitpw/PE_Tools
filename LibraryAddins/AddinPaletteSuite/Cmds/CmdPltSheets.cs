using AddinPaletteSuite.Core;
using PeUi.Core;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace AddinPaletteSuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdPltSheets : BaseCmdPalette<ViewSheet, SheetPaletteItem> {
    public override string TypeName => "sheet";

    public override IEnumerable<SheetPaletteItem> GetItems(IEnumerable<ViewSheet> sheets, Document doc) =>
        sheets.OrderBy(s => s.SheetNumber)
            .Select(sheet => new SheetPaletteItem(sheet));

    public override string GetPersistenceKey(SheetPaletteItem item) => item.Sheet.Id.ToString();

    public override IEnumerable<PaletteAction<SheetPaletteItem>> GetActions(UIApplication uiApp) =>
        new List<PaletteAction<SheetPaletteItem>> {
            new() {
                Name = "Open Sheet",
                Execute = item => uiApp.ActiveUIDocument.ActiveView = item.Sheet,
                CanExecute = item => item != null && item.Sheet.CanBePrinted
            }
        };
}

/// <summary>
///     Adapter that wraps Revit ViewSheet to implement ISelectableItem
/// </summary>
public class SheetPaletteItem(ViewSheet sheet) : IPaletteListItem {
    public ViewSheet Sheet { get; } = sheet;
    public string TextPrimary => $"{this.Sheet.SheetNumber} - {this.Sheet.Name}";

    public string TextSecondary => string.Empty;

    public string TextPill {
        get {
            var views = this.GetViewInfo();
            return views.Count == 0 ? string.Empty : $"{views.Count} views";
        }
    }

    public string TextInfo {
        get {
            var views = this.GetViewInfo();
            var viewText = views.Count == 0
                ? "None"
                : string.Join("\n  ", views.Select(v => $"{v.type} - {v.name}"));
            return $"Id: {this.Sheet.Id}" +
                   $"\nPlaced Views:\n\t{viewText}";
        }
    }

    public BitmapImage Icon => null;
    public Color? ItemColor => null;

    public List<(string type, string name)> GetViewInfo() {
        var viewInfo = new List<(string type, string name)>();
        foreach (var viewId in this.Sheet.GetAllPlacedViews()) {
            if (this.Sheet.Document.GetElement(viewId) is View view)
                viewInfo.Add((view.ViewType.ToString(), view.Name));
        }

        return viewInfo;
    }
}