using AddinCmdPalette.Actions;
using AddinCmdPalette.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;
using System.Windows.Media.Imaging;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdOpenSheet : BaseCmdPalette {
    public override string TypeName => "sheet";

    public override IEnumerable<ISelectableItem> GetItems(Document doc) =>
        new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .OrderBy(s => s.SheetNumber)
            .ToList()
            .Select(sheet => new SheetPaletteItem(sheet));

    public override string GetPersistenceKey(ISelectableItem item) {
        if (item is SheetPaletteItem sheetItem)
            return sheetItem.Sheet.Id.ToString();
        return item.PrimaryText;
    }

    public override IEnumerable<PaletteAction> GetActions(UIApplication uiApp) =>
        new List<PaletteAction> {
            new() {
                Name = "Open Sheet",
                ExecuteAsync = async item => {
                    if (item is SheetPaletteItem sheetItem) {
                        try {
                            uiApp.ActiveUIDocument.ActiveView = sheetItem.Sheet;
                        } catch (Exception ex) {
                            throw new InvalidOperationException(
                                $"Failed to open sheet '{sheetItem.Sheet.Name}': {ex.Message}");
                        }
                    }
                },
                CanExecute = item => item is SheetPaletteItem
            }
        };
}

/// <summary>
///     Adapter that wraps Revit ViewSheet to implement ISelectableItem
/// </summary>
public partial class SheetPaletteItem(ViewSheet sheet) : ObservableObject, ISelectableItem {
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double _searchScore;
    public ViewSheet Sheet { get; } = sheet;
    public string PrimaryText => $"{this.Sheet.SheetNumber} - {this.Sheet.Name}";

    public string SecondaryText => string.Empty;

    public string PillText {
        get {
            var views = this.GetViewInfo();
            return views.Count == 0 ? string.Empty : $"{views.Count} views";
        }
    }

    public string TooltipText {
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

    public List<(string type, string name)> GetViewInfo() {
        var viewInfo = new List<(string type, string name)>();
        foreach (var viewId in this.Sheet.GetAllPlacedViews()) {
            if (this.Sheet.Document.GetElement(viewId) is View view)
                viewInfo.Add((view.ViewType.ToString(), view.Name));
        }

        return viewInfo;
    }
}