using AddinCmdPalette.Actions;
using AddinCmdPalette.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdOpenFamily : BaseCmdPalette {
    public override string TypeName => "family";

    public override IEnumerable<ISelectableItem> GetItems(Document doc) =>
        new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .OrderBy(f => f.Name)
            .ToList()
            .Select(family => new FamilyPaletteItem(family, doc));

    public override string GetPersistenceKey(ISelectableItem item) {
        if (item is FamilyPaletteItem familyItem)
            return familyItem.Family.Id.ToString();
        return item.PrimaryText;
    }

    public override IEnumerable<PaletteAction> GetActions(UIApplication uiApp) {
        var doc = uiApp.ActiveUIDocument.Document;

        return new List<PaletteAction> {
            // Default action: Open family for editing (no modifiers)
            new() {
                Name = "Edit Family",
                Execute = item => {
                    if (item is FamilyPaletteItem familyItem) {
                        try {
                            var family = familyItem.Family;
                            if (!family.IsEditable)
                                throw new InvalidOperationException($"Family '{family.Name}' is not editable.");
                            var famDoc = doc.EditFamily(family)
                                         ?? throw new InvalidOperationException(
                                             $"Failed to open family '{family.Name}' for editing.");

                            // EditFamily() opens the document in the background but doesn't activate it
                            // For families without a PathName (in-memory), we need to save first
                            if (string.IsNullOrEmpty(famDoc.PathName)) {
                                var tempPath = Path.Combine(Path.GetTempPath(), $"{famDoc.Title}_{Guid.NewGuid()}.rfa");
                                famDoc.SaveAs(tempPath);
                                _ = uiApp.OpenAndActivateDocument(tempPath);
                            } else {
                                _ = uiApp.OpenAndActivateDocument(famDoc.PathName);
                            }
                        } catch (Autodesk.Revit.Exceptions.InvalidOperationException ex) {
                            throw new InvalidOperationException(
                                $"'{familyItem.Family.Name}' document may be read-only or workshared.", ex);
                        } catch (Exception ex) {
                            throw new InvalidOperationException(
                                $"'{familyItem.Family.Name}' failed to open for editing: {ex.Message}", ex);
                        }
                    }
                },
                CanExecute = item => {
                    if (item is FamilyPaletteItem familyItem)
                        return familyItem.Family.IsEditable;
                    return false;
                }
            },
            // Ctrl+Click: Select all instances of the family
            new() {
                Name = "Select Instances",
                Modifiers = ModifierKeys.Control,
                MouseButton = MouseButton.Left,
                Execute = item => {
                    if (item is FamilyPaletteItem familyItem) {
                        try {
                            var instances = new FilteredElementCollector(doc)
                                .OfClass(typeof(FamilyInstance))
                                .Cast<FamilyInstance>()
                                .Where(fi => fi.Symbol.Family.Id == familyItem.Family.Id)
                                .Select(fi => fi.Id)
                                .ToList();

                            uiApp.ActiveUIDocument.Selection.SetElementIds(instances);
                        } catch (Exception ex) {
                            throw new InvalidOperationException(
                                $"Failed to select instances of '{familyItem.Family.Name}': {ex.Message}",
                                ex
                            );
                        }
                    }
                },
                CanExecute = item => item is FamilyPaletteItem
            }
        };
    }
}

/// <summary>
///     Adapter that wraps Revit Family to implement ISelectableItem
/// </summary>
public partial class FamilyPaletteItem : ObservableObject, ISelectableItem {
    private readonly Document _doc;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double _searchScore;

    public FamilyPaletteItem(Family family, Document doc) {
        this.Family = family;
        this._doc = doc;
    }

    /// <summary> Access to underlying family </summary>
    public Family Family { get; }

    public string PrimaryText => this.Family.Name;

    public string SecondaryText {
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

    public string PillText => this.Family.FamilyCategory?.Name ?? string.Empty;

    public string TooltipText =>
        $"{this.Family.Name}\nCategory: {this.Family.FamilyCategory?.Name}\nId: {this.Family.Id}";

    public BitmapImage Icon => null;
}