using AddinPaletteSuite.Core.Actions;
using AddinPaletteSuite.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;
using AddinPaletteSuite.Core.Ui;
namespace AddinPaletteSuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdPltSheets : BaseCmdPalette {
    public override string TypeName => "sheet";

    public override IEnumerable<IPaletteListItem> GetItems(Document doc) =>
        new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .OrderBy(s => s.SheetNumber)
            .ToList()
            .Select(sheet => new SheetPaletteItem(sheet));

    public override string GetPersistenceKey(IPaletteListItem item) {
        if (item is SheetPaletteItem sheetItem)
            return sheetItem.Sheet.Id.ToString();
        return item.PrimaryText;
    }

    public override IEnumerable<PaletteAction> GetActions(UIApplication uiApp) =>
        new List<PaletteAction> {
            new() {
                Name = "Open Sheet",
                ExecuteAsync = item => {
                    if (item is SheetPaletteItem sheetItem) {
                        try {
                            uiApp.ActiveUIDocument.ActiveView = sheetItem.Sheet;
                        } catch (Exception ex) {
                            throw new InvalidOperationException(
                                $"Failed to open sheet '{sheetItem.Sheet.Name}': {ex.Message}");
                        }
                    }
                    return Task.CompletedTask;
                },
                CanExecute = item => item is SheetPaletteItem
            }
        };
}

/// <summary>
///     Adapter that wraps Revit ViewSheet to implement ISelectableItem
/// </summary>
public partial class SheetPaletteItem(ViewSheet sheet) : ObservableObject, IPaletteListItem {
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double _searchScore;
    [ObservableProperty] private bool _canExecute = true;
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