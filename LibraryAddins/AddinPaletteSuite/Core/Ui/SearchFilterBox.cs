#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;
using Grid = System.Windows.Controls.Grid;
using TextBox = System.Windows.Controls.TextBox;
using Binding = System.Windows.Data.Binding;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Component that combines a search TextBox with an optional filter box container
///     Built entirely in code (no XAML)
/// </summary>
public class SearchFilterBox {
    private readonly Grid _container;
    private readonly TextBox _searchTextBox;
    private readonly Grid _filterBoxContainer;

    public SearchFilterBox() {
        // Create the main container Grid
        this._container = new Grid {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        this._container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        this._container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Create the search TextBox
        this._searchTextBox = new TextBox {
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent
        };
        Grid.SetColumn(this._searchTextBox, 0);

        // Apply styling
        this.ApplySearchBoxStyles();

        // Create the filter box container Grid with small margin for spacing
        this._filterBoxContainer = new Grid {
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(this._filterBoxContainer, 1);

        // Add controls to container
        _ = this._container.Children.Add(this._searchTextBox);
        _ = this._container.Children.Add(this._filterBoxContainer);
    }

    /// <summary>
    ///     The root container Grid that holds both search and filter boxes
    /// </summary>
    public Grid Container => this._container;

    /// <summary>
    ///     The search TextBox control
    /// </summary>
    public TextBox SearchTextBox => this._searchTextBox;

    /// <summary>
    ///     The Grid container for the filter box (add FilterBox as child when filtering is enabled)
    /// </summary>
    public Grid FilterBoxContainer => this._filterBoxContainer;

    /// <summary>
    ///     Binds the search TextBox to view model properties and commands
    /// </summary>
    public void BindToViewModel(object viewModel) {
        // Bind SearchText property
        var searchTextBinding = new Binding("SearchText") {
            Source = viewModel,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        _ = this._searchTextBox.SetBinding(TextBox.TextProperty, searchTextBinding);

        // Bind navigation commands
        var moveDownBinding = new KeyBinding {
            Key = Key.Down
        };
        _ = BindingOperations.SetBinding(moveDownBinding, InputBinding.CommandProperty,
            new Binding("MoveSelectionDownCommand") { Source = viewModel });
        _ = this._searchTextBox.InputBindings.Add(moveDownBinding);

        var moveUpBinding = new KeyBinding {
            Key = Key.Up
        };
        _ = BindingOperations.SetBinding(moveUpBinding, InputBinding.CommandProperty,
            new Binding("MoveSelectionUpCommand") { Source = viewModel });
        _ = this._searchTextBox.InputBindings.Add(moveUpBinding);
    }

    private void ApplySearchBoxStyles() {
        // Load the CleanTextBoxStyle from resources
        var resourceDict = new ResourceDictionary {
            Source = new Uri("pack://application:,,,/PE_Tools;component/addinpalettesuite/core/ui/wpfuiresources.xaml",
                UriKind.Absolute)
        };

        Style? baseStyle = null;
        if (resourceDict["CleanTextBoxStyle"] is Style cleanTextBoxStyle) {
            baseStyle = cleanTextBoxStyle;
        }

        // Create style based on CleanTextBoxStyle
        var textBoxStyle = new Style(typeof(TextBox), baseStyle);
        this._searchTextBox.Style = textBoxStyle;

        // Set theme-aware resource references directly on the TextBox
        this._searchTextBox.SetResourceReference(TextBox.CaretBrushProperty, "TextFillColorSecondaryBrush");
        this._searchTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextFillColorPrimaryBrush");

        // Apply typography style
        ThemeManager.ApplyTypographyStyle(this._searchTextBox, FontTypography.Body);
        this._searchTextBox.FocusVisualStyle = null;
    }
}

