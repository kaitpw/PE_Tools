using AddinCmdPalette.Actions;
using AddinCmdPalette.Core;
using AddinCmdPalette.Services;
using PeServices.Storage;

namespace PE_Tools;

/// <summary>
///     Base class for commands that open palette windows
/// </summary>
[Transaction(TransactionMode.Manual)]
public abstract class CmdOpenPaletteBase : IExternalCommand {
    /// <summary>
    ///     Gets the palette type name for error messages (e.g., "schedule", "view", "sheet", "family")
    /// </summary>
    protected abstract string PaletteTypeName { get; }

    /// <summary>
    ///     Gets the palette window title
    /// </summary>
    protected abstract string PaletteTitle { get; }

    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        try {
            var uiapp = commandData.Application;

            // Create persistence service
            var persistence = new Storage(this.GetType().Name);

            // Create and show palette
            var palette = this.CreatePalette(uiapp, persistence);
            palette.Show();

            return Result.Succeeded;
        } catch (Exception ex) {
            throw new InvalidOperationException($"Error opening {this.PaletteTypeName} palette: {ex.Message}");
        }
    }

    /// <summary>
    ///     Gets the selectable items for the palette
    /// </summary>
    protected abstract IEnumerable<ISelectableItem> GetSelectableItems(UIApplication uiApp);

    /// <summary>
    ///     Gets the search filter service for the palette
    /// </summary>
    protected abstract SearchFilterService GetSearchFilterService(Storage persistence,
        IEnumerable<ISelectableItem> items);

    /// <summary>
    ///     Gets the actions for the palette
    /// </summary>
    protected abstract IEnumerable<PaletteAction> GetActions(UIApplication uiApp);

    /// <summary>
    ///     Creates the palette window with command-specific configuration
    /// </summary>
    protected SelectablePalette CreatePalette(UIApplication uiApp, Storage persistence) {
        var selectableItems = this.GetSelectableItems(uiApp).ToList();
        var searchService = this.GetSearchFilterService(persistence, selectableItems);
        var actions = this.GetActions(uiApp).ToList();

        // Create view model
        var viewModel = new SelectablePaletteViewModel(selectableItems, searchService);

        // Create and return palette window
        return new SelectablePalette(viewModel, actions) { Title = this.PaletteTitle };
    }
}