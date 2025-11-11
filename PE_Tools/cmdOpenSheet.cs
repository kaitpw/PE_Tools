using AddinCmdPalette.Actions;
using AddinCmdPalette.Core;
using AddinCmdPalette.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using PeServices.Storage;
using System.Windows.Media.Imaging;
using InvalidOperationException = Autodesk.Revit.Exceptions.InvalidOperationException;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdOpenSheet : CmdOpenPaletteBase {
    protected override string PaletteTypeName => "sheet";
    protected override string PaletteTitle => "Open Sheet";

    protected override IEnumerable<ISelectableItem> GetSelectableItems(UIApplication uiApp) {
        var doc = uiApp.ActiveUIDocument.Document;

        // Get all sheet views
        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .OrderBy(s => s.SheetNumber)
            .ToList();

        // Convert to ISelectableItem adapters
        return sheets
            .Select(sheet => new SheetPaletteItem(sheet));
    }

    protected override SearchFilterService GetSearchFilterService(Storage persistence,
        IEnumerable<ISelectableItem> items) =>
        new(
            persistence,
            item => {
                if (item is SheetPaletteItem sheetItem)
                    return sheetItem.Sheet.Id.ToString();
                return item.PrimaryText;
            },
            "SheetPalette"
        );

    protected override IEnumerable<PaletteAction> GetActions(UIApplication uiApp) =>
        new List<PaletteAction> {
            new() {
                Name = "Open Sheet",
                ExecuteAsync = async item => {
                    if (item is SheetPaletteItem sheetItem) {
                        try {
                            // Must run on main thread, not Task.Run
                            // ViewSheet inherits from View, so setting ActiveView works
                            uiApp.ActiveUIDocument.ActiveView = sheetItem.Sheet;
                        } catch (InvalidOperationException ex) {
                            throw new System.InvalidOperationException(
                                $"Cannot open sheet '{sheetItem.Sheet.Name}'. The document may be read-only or the sheet cannot be activated.",
                                ex
                            );
                        } catch (Exception ex) {
                            throw new System.InvalidOperationException(
                                $"Failed to open sheet '{sheetItem.Sheet.Name}': {ex.Message}",
                                ex
                            );
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
public partial class SheetPaletteItem : ObservableObject, ISelectableItem {
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double _searchScore;

    public SheetPaletteItem(ViewSheet sheet) => this.Sheet = sheet;

    /// <summary> Access to underlying sheet view </summary>
    public ViewSheet Sheet { get; }

    public string PrimaryText => $"{this.Sheet.SheetNumber} - {this.Sheet.Name}";

    public string SecondaryText => string.Empty;

    public string PillText => string.Empty;

    public string TooltipText => $"{this.Sheet.Name}\nSheet Number: {this.Sheet.SheetNumber}\nId: {this.Sheet.Id}";

    public BitmapImage Icon => null;
}