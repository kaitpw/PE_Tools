using AddinPaletteSuite.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Generic ViewModel for the SelectablePalette window
/// </summary>
public partial class SelectablePaletteViewModel<TItem> : ObservableObject where TItem : BaseObservableListItem, IPaletteListItem {
    private readonly List<TItem> _allItems;
    private readonly SearchFilterService<TItem> _searchService;

    /// <summary> Current search text </summary>
    [ObservableProperty] private string _searchText = string.Empty;

    /// <summary> Currently selected index in the filtered list </summary>
    [ObservableProperty] private int _selectedIndex = -1;

#nullable enable
    /// <summary> Currently selected item </summary>
    [ObservableProperty] private TItem? _selectedItem;
#nullable disable

    public SelectablePaletteViewModel(
        IEnumerable<TItem> items,
        SearchFilterService<TItem> searchService
    ) {
        this._allItems = items.ToList();
        this._searchService = searchService;

        this._searchService.LoadUsageData();
        this.FilteredItems = new ObservableCollection<TItem>();
        this.FilterItems();

        if (this.FilteredItems.Count > 0)
            this.SelectedIndex = 0;
    }

    /// <summary> Filtered list of items based on search text </summary>
    public ObservableCollection<TItem> FilteredItems { get; }

    /// <summary> Event raised when filtered items collection changes </summary>
    public event EventHandler FilteredItemsChanged;

    [RelayCommand]
    private void MoveSelectionUp() {
        if (this.SelectedIndex > 0) this.SelectedIndex--;
    }

    [RelayCommand]
    private void MoveSelectionDown() {
        if (this.SelectedIndex < this.FilteredItems.Count - 1) this.SelectedIndex++;
    }

    [RelayCommand]
    private void ClearSearch() => this.SearchText = string.Empty;

    /// <summary>
    ///     Filters items based on current search text
    /// </summary>
    private void FilterItems() {
        var filtered = this._searchService.Filter(this.SearchText, this._allItems);

        this.FilteredItems.Clear();
        foreach (var item in filtered)
            this.FilteredItems.Add(item);

        // Reset selection to first item
        this.SelectedIndex = this.FilteredItems.Count > 0 ? 0 : -1;

        // Notify that filtered items have changed
        this.FilteredItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Records usage of the selected item
    /// </summary>
    public void RecordUsage() {
        if (this.SelectedItem != null)
            this._searchService.RecordUsage(this.SelectedItem);
    }

    #region Property Change Handlers

    partial void OnSearchTextChanged(string value) => this.FilterItems();

    partial void OnSelectedItemChanged(TItem value) {
        // Clear previous selection
        foreach (var item in this.FilteredItems) {
            if (item.TextPrimary != value.TextPrimary)
                item.IsSelected = false;
        }

        // Set new selection
        if (value != null) value.IsSelected = true;
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