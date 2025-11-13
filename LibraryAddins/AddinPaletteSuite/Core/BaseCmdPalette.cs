using AddinPaletteSuite.Core.Ui;
using AddinPaletteSuite.Core.Actions;
using AddinPaletteSuite.Core.Services;
using PeServices.Storage;

namespace AddinPaletteSuite.Core;

/// <summary>
///     Base class for commands that open palette windows
/// </summary>
public abstract class BaseCmdPalette : IExternalCommand {
    public abstract string TypeName { get; }
    public string Title => $"{char.ToUpper(this.TypeName[0])}{this.TypeName[1..]} Palette";

    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        try {
            var uiapp = commandData.Application;
            var doc = uiapp.ActiveUIDocument.Document;
            var persistence = new Storage(this.GetType().Name);
            var selectableItems = this.GetItems(doc).ToList();
            var searchService = new SearchFilterService(persistence, this.GetPersistenceKey);
            var actions = this.GetActions(uiapp).ToList();
            var viewModel = new SelectablePaletteViewModel(selectableItems, searchService);
            var palette = new SelectablePalette(viewModel, actions);
            var window = new EphemeralWindow(palette, this.Title);
            window.Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            throw new InvalidOperationException($"Error opening {this.Title} palette: {ex.Message}");
        }
    }

    public abstract string GetPersistenceKey(IPaletteListItem item);
    public abstract IEnumerable<IPaletteListItem> GetItems(Document doc);
    public abstract IEnumerable<PaletteAction> GetActions(UIApplication uiApp);
}