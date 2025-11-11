using AddinCmdPalette.Core;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace AddinCmdPalette.Families;

/// <summary>
///     Adapter that wraps Revit Family to implement ISelectableItem
/// </summary>
public partial class FamilyPaletteItem : ObservableObject, ISelectableItem {
    private readonly Family _family;
    private readonly Document _doc;

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double _searchScore;

    public FamilyPaletteItem(Family family, Document doc) {
        this._family = family;
        this._doc = doc;
    }

    public string PrimaryText => this._family.Name;

    public string SecondaryText {
        get {
            // Get list of family type names
            var symbolIds = this._family.GetFamilySymbolIds();
            var typeNames = symbolIds
                .Select(id => this._doc.GetElement(id))
                .OfType<FamilySymbol>()
                .Select(symbol => symbol.Name)
                .OrderBy(name => name)
                .ToList();
            
            return string.Join(", ", typeNames);
        }
    }

    public string PillText {
        get {
            // Show category as pill
            return this._family.FamilyCategory?.Name ?? string.Empty;
        }
    }

    public string TooltipText => $"{this._family.Name}\nCategory: {this._family.FamilyCategory?.Name}\nId: {this._family.Id}";

    public BitmapImage Icon => null;

    /// <summary> Access to underlying family </summary>
    public Family Family => this._family;
}

