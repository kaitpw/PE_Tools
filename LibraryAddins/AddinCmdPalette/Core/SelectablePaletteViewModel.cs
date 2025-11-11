using AddinCmdPalette.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AddinCmdPalette.Core;

/// <summary>
///     Generic ViewModel for the SelectablePalette window
/// </summary>
public partial class SelectablePaletteViewModel : ObservableObject {
    private readonly List<ISelectableItem> _allItems;
    private readonly SearchFilterService _searchService;

    /// <summary> Current search text </summary>
    [ObservableProperty] private string _searchText = string.Empty;

    /// <summary> Currently selected index in the filtered list </summary>
    [ObservableProperty] private int _selectedIndex = -1;

#nullable enable
    /// <summary> Currently selected item </summary>
    [ObservableProperty] private ISelectableItem? _selectedItem;
#nullable disable

    public SelectablePaletteViewModel(
        IEnumerable<ISelectableItem> items,
        SearchFilterService searchService
    ) {
        this._allItems = items.ToList();
        this._searchService = searchService;

        // Load usage data if service supports it
        this._searchService.LoadUsageData(this._allItems);

        this.FilteredItems = new ObservableCollection<ISelectableItem>();

        // Initial load - show all items
        this.FilterCommands();

        // Select first item by default
        if (this.FilteredItems.Count > 0)
            this.SelectedIndex = 0;
    }

    /// <summary> Filtered list of items based on search text </summary>
    public ObservableCollection<ISelectableItem> FilteredItems { get; }

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
    private void FilterCommands() {
        var filtered = this._searchService.Filter(this.SearchText, this._allItems);

        this.FilteredItems.Clear();
        foreach (var item in filtered)
            this.FilteredItems.Add(item);

        // Reset selection to first item
        this.SelectedIndex = this.FilteredItems.Count > 0 ? 0 : -1;
    }

    /// <summary>
    ///     Records usage of the selected item
    /// </summary>
    public void RecordUsage() {
        if (this.SelectedItem != null)
            this._searchService.RecordUsage(this.SelectedItem);
    }

    #region Property Change Handlers

    partial void OnSearchTextChanged(string value) => this.FilterCommands();

    partial void OnSelectedItemChanged(ISelectableItem value) {
        // Clear previous selection
        foreach (var item in this.FilteredItems) {
            if (item != value)
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