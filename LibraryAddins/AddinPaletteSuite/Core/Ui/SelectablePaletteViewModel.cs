using AddinPaletteSuite.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Generic ViewModel for the SelectablePalette window with optional filtering support
/// </summary> 
public partial class SelectablePaletteViewModel<TItem> : ObservableObject
    where TItem : BaseObservableListItem, IPaletteListItem {
    private readonly List<TItem> _allItems;
    private readonly Func<TItem, string>? _filterKeySelector;
    private readonly SearchFilterService<TItem> _searchService;
    private readonly DispatcherTimer _debounceTimer;

    /// <summary> Current search text </summary>
    [ObservableProperty] private string _searchText = string.Empty;

    private string _selectedFilterValue = string.Empty;

    /// <summary> Currently selected index in the filtered list </summary>
    [ObservableProperty] private int _selectedIndex = -1;

#nullable enable
    /// <summary> Currently selected item </summary>
    [ObservableProperty] private TItem? _selectedItem;

    /// <summary> Previously selected item for efficient selection updates </summary>
    private TItem? _previousSelectedItem;
#nullable disable

    public SelectablePaletteViewModel(
        IEnumerable<TItem> items,
        SearchFilterService<TItem> searchService,
        Func<TItem, string>? filterKeySelector = null
    ) {
        this._allItems = items.ToList();
        this._searchService = searchService;
        this._filterKeySelector = filterKeySelector;

        // Initialize debounce timer (200ms delay)
        this._debounceTimer = new DispatcherTimer {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        this._debounceTimer.Tick += (_, _) => {
            this._debounceTimer.Stop();
            this.FilterItems();
        };

        this._searchService.LoadUsageData();

        // Build search cache for all items (pre-compute lowercase strings and metadata)
        this._searchService.BuildSearchCache(this._allItems);

        this.FilteredItems = new ObservableCollection<TItem>();

        // Initialize filter values if filtering is enabled
        if (this._filterKeySelector != null) {
            this.AvailableFilterValues = new ObservableCollection<string>(
                this._allItems
                    .Select(this._filterKeySelector)
                    .Where(key => !string.IsNullOrEmpty(key))
                    .Distinct()
                    .OrderBy(key => key)
            );
        }

        this.FilterItems();

        if (this.FilteredItems.Count > 0)
            this.SelectedIndex = 0;
    }

    /// <summary> Filtered list of items based on search text and optional filter </summary>
    public ObservableCollection<TItem> FilteredItems { get; }

    /// <summary> Available filter values (only populated if filtering is enabled) </summary>
    public ObservableCollection<string>? AvailableFilterValues { get; }

    /// <summary> Whether filtering is enabled for this palette </summary>
    public bool IsFilteringEnabled => this._filterKeySelector != null;

    /// <summary> Currently selected filter value </summary>
    public string SelectedFilterValue {
        get => this._selectedFilterValue;
        set {
            if (this.SetProperty(ref this._selectedFilterValue, value))
                this.FilterItems();
        }
    }

    /// <summary> Event raised when filtered items collection changes </summary>
    public event EventHandler FilteredItemsChanged;

    [RelayCommand]
    private void MoveSelectionUp() {
        if (this.FilteredItems.Count == 0) return;
        
        if (this.SelectedIndex > 0) {
            this.SelectedIndex--;
        } else {
            // Wrap to bottom
            this.SelectedIndex = this.FilteredItems.Count - 1;
        }
    }

    [RelayCommand]
    private void MoveSelectionDown() {
        if (this.FilteredItems.Count == 0) return;
        
        if (this.SelectedIndex < this.FilteredItems.Count - 1) {
            this.SelectedIndex++;
        } else {
            // Wrap to top
            this.SelectedIndex = 0;
        }
    }

    [RelayCommand]
    private void ClearSearch() => this.SearchText = string.Empty;

    /// <summary>
    ///     Filters items based on current search text and optional filter value
    /// </summary>
    private void FilterItems() {
        // First filter by filter value if one is selected and filtering is enabled
        var preFiltered = this._allItems;
        if (this._filterKeySelector != null && !string.IsNullOrEmpty(this.SelectedFilterValue)) {
            preFiltered = this._allItems
                .Where(item => this._filterKeySelector(item) == this.SelectedFilterValue)
                .ToList();
        }

        // Then apply search filter
        var filtered = this._searchService.Filter(this.SearchText, preFiltered);

        // Use efficient differential update instead of Clear/Add
        this.UpdateCollectionEfficiently(this.FilteredItems, filtered);

        // Reset selection to first item
        this.SelectedIndex = this.FilteredItems.Count > 0 ? 0 : -1;

        // Notify that filtered items have changed
        this.FilteredItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Efficiently updates an ObservableCollection to match a target list.
    ///     Uses differential updates to minimize CollectionChanged notifications.
    /// </summary>
    private void UpdateCollectionEfficiently(ObservableCollection<TItem> target, List<TItem> source) {
        // Remove items not in source (iterate backwards to avoid index shifting)
        for (var i = target.Count - 1; i >= 0; i--) {
            if (!source.Contains(target[i]))
                target.RemoveAt(i);
        }

        // Add/reorder items from source
        for (var i = 0; i < source.Count; i++) {
            if (i >= target.Count) {
                // Need to add new item
                target.Add(source[i]);
            } else if (!EqualityComparer<TItem>.Default.Equals(target[i], source[i])) {
                // Item at this position is different, update it
                target[i] = source[i];
            }
            // else: item is already in the correct position, no action needed
        }
    }

    /// <summary>
    ///     Records usage of the selected item
    /// </summary>
    public void RecordUsage() {
        if (this.SelectedItem != null)
            this._searchService.RecordUsage(this.SelectedItem);
    }

    #region Property Change Handlers

    partial void OnSearchTextChanged(string value) {
        // If search is cleared, filter immediately (no debounce)
        if (string.IsNullOrWhiteSpace(value)) {
            this._debounceTimer.Stop();
            this.FilterItems();
            return;
        }

        // Otherwise, restart debounce timer
        this._debounceTimer.Stop();
        this._debounceTimer.Start();
    }

    partial void OnSelectedItemChanged(TItem value) {
        // Only update previous and current items (O(1) instead of O(n))
        if (this._previousSelectedItem != null && this._previousSelectedItem != value)
            this._previousSelectedItem.IsSelected = false;

        if (value != null)
            value.IsSelected = true;

        this._previousSelectedItem = value;
    }

    partial void OnSelectedIndexChanged(int value) {
        // Update selected item based on index
        if (value >= 0 && value < this.FilteredItems.Count)
            this.SelectedItem = this.FilteredItems[value];
        else
            this.SelectedItem = null;
    }

    #endregion
}