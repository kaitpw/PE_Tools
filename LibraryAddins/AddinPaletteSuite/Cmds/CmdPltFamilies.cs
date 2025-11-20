#nullable enable
using AddinPaletteSuite.Core;
using PeExtensions.FamDocument;
using PeUi.Core;
using PeUi.Core.Services;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace AddinPaletteSuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdPltFamilies : BaseCmdPalette<Family, FamilyPaletteItem> {
    public override string TypeName => "family";

    public override IEnumerable<FamilyPaletteItem> GetItems(IEnumerable<Family> families, Document doc) =>
        families.Select(family => new FamilyPaletteItem(family, doc));

    public override string GetPersistenceKey(FamilyPaletteItem item) => item.Family.Id.ToString();

    /// <summary>
    ///     Enable filtering by category (TextPill property)
    /// </summary>
    protected override Func<FamilyPaletteItem, string>? GetFilterKeySelector() => item => item.TextPill;

    /// <summary>
    ///     Search both primary (family name) and secondary (family type names)
    /// </summary>
    protected override SearchConfig GetSearchConfig() => SearchConfig.PrimaryAndSecondary();

    public override IEnumerable<PaletteAction<FamilyPaletteItem>> GetActions(UIApplication uiApp) {
        var doc = uiApp.ActiveUIDocument.Document;
        var activeView = uiApp.ActiveUIDocument.ActiveView;

        return new List<PaletteAction<FamilyPaletteItem>> {
            // Default action: Open family types palette (Enter or Click)
            new() {
                Name = "Types",
                ExecuteNextPalette = item => {
                    var familyTypes = new PltFamilyTypes(item.Family);
                    familyTypes.Open(uiApp);
                },
                CanExecute = item => item != null
            },
            new() {
                Name = "Select in View",
                Modifiers = ModifierKeys.Shift,
                Execute = item => {
                    var instances = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .Where(fi => fi.Symbol.Family.Id == item.Family.Id)
                        .Select(fi => fi.Id)
                        .ToList();
                    uiApp.ActiveUIDocument.Selection.SetElementIds(instances);
                },
                CanExecute = item => item != null && !activeView.IsTemplate
                                                  && activeView.ViewType != ViewType.Legend
                                                  && activeView.ViewType != ViewType.DrawingSheet
                                                  && activeView.ViewType != ViewType.DraftingView
                                                  && activeView.ViewType != ViewType.SystemBrowser
                                                  && activeView is not ViewSchedule && item.Family.IsEditable
            },
            new() {
                Name = "Open/Edit",
                Modifiers = ModifierKeys.Control,
                Execute = item => doc.EditFamily(item.Family).GetFamilyDocument().OpenForUserEditting(uiApp),
                CanExecute = item => item != null && item.Family.IsEditable
            }
        };
    }
}

/// <summary>
///     Adapter that wraps Revit Family to implement ISelectableItem
/// </summary>
public class FamilyPaletteItem : IPaletteListItem {
    private readonly Document _doc;

    public FamilyPaletteItem(Family family, Document doc) {
        this.Family = family;
        this._doc = doc;
    }

    /// <summary> Access to underlying family </summary>
    public Family Family { get; }

    public string TextPrimary => this.Family.Name;

    public string TextSecondary {
        get {
            // Get list of family type names
            var symbolIds = this.Family.GetFamilySymbolIds();
            var typeNames = symbolIds
                .Select(this._doc.GetElement)
                .OfType<FamilySymbol>()
                .Select(symbol => symbol.Name)
                .OrderBy(name => name)
                .ToList();

            return string.Join(", ", typeNames);
        }
    }

    public string TextPill => this.Family.FamilyCategory?.Name ?? string.Empty;

    public string TextInfo =>
        $"{this.Family.Name}\nCategory: {this.Family.FamilyCategory?.Name}\nId: {this.Family.Id}";

    public BitmapImage? Icon => null;
    public Color? ItemColor => null;
}