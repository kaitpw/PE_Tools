using AddinPaletteSuite.Core.Ui;
using AddinPaletteSuite.Core.Actions;
using AddinPaletteSuite.Core.Services;
using PeServices.Storage;
using PeRevit.Ui;

namespace AddinPaletteSuite.Core;

/// <summary>
///     Base class for commands that open palette windows
/// </summary>
public abstract class BaseCmdPalette<TElement, TItem> : IExternalCommand where TElement : Element where TItem : BaseObservableListItem, IPaletteListItem {
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
            var elements = new FilteredElementCollector(doc)
                .OfClass(typeof(TElement))
                .Cast<TElement>()
                .OrderBy(f => f.Name);
            var selectableItems = this.GetItems(elements, doc);
            var searchService = new SearchFilterService<TItem>(persistence, this.GetPersistenceKey);
            var actions = this.GetActions(uiapp).ToList();
            var viewModel = new SelectablePaletteViewModel<TItem>(selectableItems, searchService);
            var palette = new SelectablePalette<TItem>(viewModel, actions);
            var window = new EphemeralWindow(palette, this.Title);
            window.Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Failed;
        }
    }

    public abstract string GetPersistenceKey(TItem item);
    public abstract IEnumerable<TItem> GetItems(IEnumerable<TElement> elements, Document doc);
    public abstract IEnumerable<PaletteAction<TItem>> GetActions(UIApplication uiApp);
}