using AddinCmdPalette.Core;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;

namespace AddinCmdPalette.Sheets;

/// <summary>
///     Adapter that wraps Revit ViewSheet to implement ISelectableItem
/// </summary>
public partial class SheetPaletteItem : ObservableObject, ISelectableItem {
    private readonly ViewSheet _sheet;

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double _searchScore;

    public SheetPaletteItem(ViewSheet sheet) {
        this._sheet = sheet;
    }

    public string PrimaryText => $"{this._sheet.SheetNumber} - {this._sheet.Name}";

    public string SecondaryText => string.Empty;

    public string PillText => string.Empty;

    public string TooltipText => $"{this._sheet.Name}\nSheet Number: {this._sheet.SheetNumber}\nId: {this._sheet.Id}";

    public BitmapImage Icon => null;

    /// <summary> Access to underlying sheet view </summary>
    public ViewSheet Sheet => this._sheet;
}

