using PeServices.Storage;
using PeUi.Components;
using PeUi.Core.Services;
using PeUi.ViewModels;

namespace PeUi.Core;

/// <summary>
///     Factory for creating palette windows using composition instead of inheritance.
///     Handles the boilerplate of wiring up SearchFilterService, PaletteViewModel, Palette, and EphemeralWindow.
/// </summary>
/// <example>
///     Basic usage with persistence and filtering:
///     <code>
///     var window = PaletteFactory.Create("Schedule Palette", items, actions,
///         new PaletteOptions&lt;SchedulePaletteItem&gt; {
///             Storage = new Storage(nameof(CmdPltSchedules)),
///             PersistenceKey = item => item.Schedule.Id.ToString(),
///             SearchConfig = SearchConfig.PrimaryAndSecondary(),
///             FilterKeySelector = item => item.TextPill
///         });
///     window.Show();
///     </code>
/// </example>
/// <example>
///     Minimal usage (search enabled, no persistence):
///     <code>
///     var window = PaletteFactory.Create("My Palette", items, actions);
///     window.Show();
///     </code>
/// </example>
public static class PaletteFactory {
    /// <summary>
    ///     Creates an EphemeralWindow containing a fully configured palette.
    /// </summary>
    /// <typeparam name="TItem">The palette item type (must implement IPaletteListItem)</typeparam>
    /// <param name="title">Window title displayed in the floating pill</param>
    /// <param name="items">Items to display in the palette list</param>
    /// <param name="actions">Actions available for items (first action with no modifiers is the default)</param>
    /// <param name="options">Optional configuration. If null, uses defaults (search enabled, no persistence)</param>
    /// <returns>An EphemeralWindow ready to show</returns>
    public static EphemeralWindow Create<TItem>(
        string title,
        IEnumerable<TItem> items,
        List<PaletteAction<TItem>> actions,
        PaletteOptions<TItem> options = null
    ) where TItem : class, IPaletteListItem {
        options ??= new PaletteOptions<TItem>();

        // Create search service - with or without persistence based on configuration
        var searchService = options.Storage != null && options.PersistenceKey != null
            ? new SearchFilterService<TItem>(options.Storage, options.PersistenceKey, options.SearchConfig)
            : new SearchFilterService<TItem>(options.SearchConfig);

        // Create view model
        var viewModel = new PaletteViewModel<TItem>(items, searchService, options.FilterKeySelector);
        options.ViewModelMutator?.Invoke(viewModel);

        // Create palette - hide search box if search is disabled
        var isSearchDisabled = options.SearchConfig == null;
        var palette = new Palette(isSearchBoxHidden: isSearchDisabled);

        // Create Ctrl-release callback if provided
        // Pass viewModel reference so callback can read current SelectedItem when Ctrl is released
        Action onCtrlReleased = null;
        if (options.OnCtrlReleased != null) {
            var vmRef = viewModel; // Capture viewModel reference
            onCtrlReleased = options.OnCtrlReleased(vmRef);
        }

        palette.Initialize(viewModel, actions, options.CustomKeyBindings, onCtrlReleased);
        return new EphemeralWindow(palette, title);
    }
}

/// <summary>
///     Configuration options for <see cref="PaletteFactory.Create{TItem}"/>.
///     All properties are optional - use only what you need.
/// </summary>
/// <typeparam name="TItem">The palette item type</typeparam>
public class PaletteOptions<TItem> where TItem : class, IPaletteListItem {
    /// <summary>
    ///     Storage instance for persisting usage data. Required for persistence to work.
    ///     Default: null (no persistence)
    /// </summary>
    /// <example>
    ///     <code>Storage = new Storage(nameof(MyCmdClass))</code>
    /// </example>
    public Storage Storage { get; init; }

    /// <summary>
    ///     Function that returns a unique key for each item, used for persistence.
    ///     Required (along with Storage) for persistence to work.
    ///     Default: null (no persistence)
    /// </summary>
    /// <example>
    ///     <code>PersistenceKey = item => item.Element.Id.ToString()</code>
    /// </example>
    public Func<TItem, string> PersistenceKey { get; init; }

    /// <summary>
    ///     Search configuration controlling which fields to search and scoring weights.
    ///     Default: <see cref="SearchConfig.Default()"/> (searches TextPrimary only).
    ///     Set to null to disable search entirely (hides the search box).
    /// </summary>
    /// <example>
    ///     <code>
    ///     // Search both name and description:
    ///     SearchConfig = SearchConfig.PrimaryAndSecondary()
    ///     
    ///     // Disable search entirely:
    ///     SearchConfig = null
    ///     </code>
    /// </example>
    public SearchConfig SearchConfig { get; init; } = SearchConfig.Default();

    /// <summary>
    ///     Function that extracts a filter category from each item, enabling dropdown filtering.
    ///     Default: null (filtering disabled)
    /// </summary>
    /// <example>
    ///     <code>
    ///     // Filter by view type:
    ///     FilterKeySelector = item => item.View.ViewType.ToString()
    ///     
    ///     // Filter by category:
    ///     FilterKeySelector = item => item.TextPill
    ///     </code>
    /// </example>
    public Func<TItem, string> FilterKeySelector { get; init; }

    /// <summary>
    ///     Custom keyboard bindings for navigation.
    ///     Default: null (uses only built-in arrow key navigation)
    /// </summary>
    /// <example>
    ///     <code>
    ///     var keys = new CustomKeyBindings();
    ///     keys.Add(Key.OemTilde, NavigationAction.MoveDown, ModifierKeys.Control);
    ///     CustomKeyBindings = keys;
    ///     </code>
    /// </example>
    public CustomKeyBindings CustomKeyBindings { get; init; }

    /// <summary>
    ///     Callback to mutate the view model after creation but before palette initialization.
    ///     Useful for setting initial selection state.
    ///     Default: null (no mutation)
    /// </summary>
    /// <example>
    ///     <code>
    ///     // Select second item for MRU-style palettes:
    ///     ViewModelMutator = vm => { if (vm.FilteredItems.Count > 1) vm.SelectedIndex = 1; }
    ///     </code>
    /// </example>
    public Action<PaletteViewModel<TItem>> ViewModelMutator { get; init; }

    /// <summary>
    ///     Factory function that receives the view model and returns an action to execute when Ctrl is released.
    ///     Used for "hold Ctrl to browse, release to select" behavior (like Alt+Tab).
    ///     The returned action should read the current SelectedItem when executed (not when created).
    ///     Default: null (no Ctrl-release behavior)
    /// </summary>
    /// <example>
    ///     <code>
    ///     OnCtrlReleased = vm => () => {
    ///         // Read current SelectedItem when Ctrl is released (not at window creation)
    ///         var selected = vm.SelectedItem;
    ///         if (selected?.View != null)
    ///             uiapp.ActiveUIDocument.ActiveView = selected.View;
    ///     }
    ///     </code>
    /// </example>
    public Func<PaletteViewModel<TItem>, Action> OnCtrlReleased { get; init; }
}

