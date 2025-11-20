using AddinPaletteSuite.Core;
using PeRevit.Ui;
using PeUi.Core;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace AddinPaletteSuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class PltFamilyTypes(Family family) : BaseCmdPalette<FamilySymbol, FamilyTypePaletteItem> {
    public Family Family { get; } = family;
    public override string TypeName => "family type";

    public override IEnumerable<FamilyTypePaletteItem>
        GetItems(IEnumerable<FamilySymbol> familySymbols, Document doc) =>
        familySymbols.Where(f => f.Family.Id == this.Family.Id)
            .Select(famSymbol => new FamilyTypePaletteItem(famSymbol));

    public override string GetPersistenceKey(FamilyTypePaletteItem item) => item.FamilySymbol.Id.ToString();

    public override IEnumerable<PaletteAction<FamilyTypePaletteItem>> GetActions(UIApplication uiApp) {
        var activeView = uiApp.ActiveUIDocument.ActiveView;

        return new List<PaletteAction<FamilyTypePaletteItem>> {
            new() {
                Name = "Place",
                Execute = item => {
                    var symbol = item.FamilySymbol;
                    if (!symbol.IsActive) symbol.Activate();

                    try {
                        uiApp.ActiveUIDocument.PromptForFamilyInstancePlacement(symbol);
                    } catch (OperationCanceledException) {
                        // User canceled placement - this is expected behavior, not an error
                    } catch (Exception ex) {
                        new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
                    }
                },
                CanExecute = item => {
                    if (item == null) return false;

                    // Check if active view is valid for placing families
                    // Same logic as CmdPltViews - exclude templates, legends, sheets, schedules, etc.
                    return !activeView.IsTemplate
                           && activeView.ViewType != ViewType.Legend
                           && activeView.ViewType != ViewType.DrawingSheet
                           && activeView.ViewType != ViewType.DraftingView
                           && activeView.ViewType != ViewType.SystemBrowser
                           && activeView is not ViewSchedule;
                }
            }
        };
    }
}

public class FamilyTypePaletteItem(FamilySymbol familySymbol) : IPaletteListItem {
    public FamilySymbol FamilySymbol { get; } = familySymbol;
    public string TextPrimary => this.FamilySymbol.Name;
    public string TextSecondary => string.Empty;
    public string TextPill => string.Empty;

    public string TextInfo =>
        $"{this.FamilySymbol.Name} - {this.FamilySymbol.Family.Name} - {this.FamilySymbol.Family.FamilyCategory?.Name ?? string.Empty}";

    public BitmapImage Icon => null;
    public Color? ItemColor => null;
}