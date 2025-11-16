#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Grid = System.Windows.Controls.Grid;
using TextBox = Wpf.Ui.Controls.TextBox;
using Binding = System.Windows.Data.Binding;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Component that combines a search TextBox with an optional FilterBox
///     Self-contained with automatic filter integration when needed
/// </summary>
public class SearchFilterBox<TViewModel> where TViewModel : class {
    private readonly FilterBox<TViewModel>? _filterBox;

    public SearchFilterBox(TViewModel viewModel,
        string? availableValuesPropertyName = null,
        string? selectedValuePropertyName = null) {
        // Create the main container Grid
        this.Container = new Grid {
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center
        };
        this.Container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        this.Container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Create the search TextBox
        this.SearchTextBox = new TextBox();
        Grid.SetColumn(this.SearchTextBox, 0);
        ThemeManager.LoadWpfUiResources(this.SearchTextBox);
        _ = this.Container.Children.Add(this.SearchTextBox);

        // Bind search text
        this.BindSearchToViewModel(viewModel);

        // If filter properties are provided, create and add FilterBox
        if (!string.IsNullOrEmpty(availableValuesPropertyName) && !string.IsNullOrEmpty(selectedValuePropertyName)) {
            this._filterBox = new FilterBox<TViewModel>(viewModel);
            this._filterBox.BindToViewModel(availableValuesPropertyName, selectedValuePropertyName);
            this._filterBox.ReturnFocusTarget = this.SearchTextBox;
            this._filterBox.ExitRequested += (_, _) => _ = this.SearchTextBox.Focus();

            Grid.SetColumn(this._filterBox, 1);
            _ = this.Container.Children.Add(this._filterBox);
        }
    }

    /// <summary>
    ///     The root container Grid
    /// </summary>
    public Grid Container { get; }

    /// <summary>
    ///     The search TextBox control (for event binding)
    /// </summary>
    public TextBox SearchTextBox { get; }

    /// <summary>
    ///     Whether this SearchFilterBox has filtering enabled
    /// </summary>
    public bool HasFilter => this._filterBox != null;

    /// <summary>
    ///     Focuses the filter box if filtering is enabled
    /// </summary>
    public void FocusFilter() => this._filterBox?.Focus();

    /// <summary>
    ///     Checks if the given element is inside the filter box
    /// </summary>
    public bool IsFilterFocused(DependencyObject element) =>
        this._filterBox != null && this._filterBox.IsAncestorOf(element);

    private void BindSearchToViewModel(TViewModel viewModel) {
        // Bind SearchText property
        var searchTextBinding = new Binding("SearchText") {
            Source = viewModel, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        _ = this.SearchTextBox.SetBinding(TextBox.TextProperty, searchTextBinding);

        // Bind navigation commands
        var moveDownBinding = new KeyBinding { Key = Key.Down };
        _ = BindingOperations.SetBinding(moveDownBinding, InputBinding.CommandProperty,
            new Binding("MoveSelectionDownCommand") { Source = viewModel });
        _ = this.SearchTextBox.InputBindings.Add(moveDownBinding);

        var moveUpBinding = new KeyBinding { Key = Key.Up };
        _ = BindingOperations.SetBinding(moveUpBinding, InputBinding.CommandProperty,
            new Binding("MoveSelectionUpCommand") { Source = viewModel });
        _ = this.SearchTextBox.InputBindings.Add(moveUpBinding);
    }
}