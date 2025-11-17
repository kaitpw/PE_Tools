using AddinPaletteSuite.Core;
using System.Windows.Media.Imaging;

namespace AddinPaletteSuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdPltAllViews : BaseCmdPalette<View, AllViewPaletteItem> {
    public override string TypeName => "all views";

    public override IEnumerable<AllViewPaletteItem> GetItems(IEnumerable<View> views, Document doc) =>
        views.Select(view => new AllViewPaletteItem(view));

    public override string GetPersistenceKey(AllViewPaletteItem item) => item.View.Id.ToString();

    protected override Func<AllViewPaletteItem, string>? GetFilterKeySelector() =>
        item => item.View.ViewType.ToString();

    /// <summary>
    ///     TODO: Eventually search all fields (Primary, Secondary, Pill, Info) for comprehensive search
    ///     For now, use default (TextPrimary only)
    /// </summary>
    protected override SearchConfig GetSearchConfig() => SearchConfig.Default();

    public override IEnumerable<PaletteAction<AllViewPaletteItem>> GetActions(UIApplication uiApp) =>
        new List<PaletteAction<AllViewPaletteItem>> {
            new() {
                Name = "Open View",
                Execute = item => uiApp.ActiveUIDocument.ActiveView = item.View,
                CanExecute = item => item != null && item.View.CanBePrinted
            }
        };
}

/// <summary>
///     Adapter that wraps Revit View to implement ISelectableItem for all views (no filtering)
/// </summary>
public class AllViewPaletteItem(View view) : BaseObservableListItem, IPaletteListItem {
    public View View { get; } = view;
    public string TextPrimary => this.View.Name;
    public string TextSecondary => string.Empty;
    public string TextPill => this.View.ViewType.ToString();
    public string TextInfo => $"View Type: {this.View.ViewType}\nId: {this.View.Id}";
    public BitmapImage Icon => null;
    public System.Windows.Media.Color? ItemColor => null;
}

