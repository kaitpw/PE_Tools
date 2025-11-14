using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Pure C# list item control for SelectableListView with no XAML dependency.
///     Implements shadcn-inspired styling using ThemeManager.
/// </summary>
public class SelectableListViewItem : Border {
    private readonly Image _iconImage;
    private readonly TextBlock _primaryText;
    private readonly TextBlock _secondaryText;
    private readonly Border _pillBorder;
    private readonly TextBlock _pillText;
    private readonly System.Windows.Controls.Grid _textGrid;
    private readonly System.Windows.Controls.Grid _contentGrid;

    public SelectableListViewItem() {
        this.CornerRadius = ThemeManager.ItemCornerRadius;
        this.DataContextChanged += this.OnDataContextChanged;

        // Create content grid
        this._contentGrid = new System.Windows.Controls.Grid {
            ColumnDefinitions = {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        this.Child = this._contentGrid;

        this._iconImage = new Image {
            Width = (double)UiSz.ll,
            Height = (double)UiSz.ll, 
            Margin = new Thickness(0, 0, (double)UiSz.l, 0),
            Opacity = ThemeManager.IconOpacity,
            VerticalAlignment = VerticalAlignment.Center
        };
        System.Windows.Controls.Grid.SetColumn(this._iconImage, 0);
        _ = this._contentGrid.Children.Add(this._iconImage);

        // Create Text Stack Grid
        this._textGrid = new System.Windows.Controls.Grid {
            VerticalAlignment = VerticalAlignment.Center
        };
        this._textGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        this._textGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        System.Windows.Controls.Grid.SetColumn(this._textGrid, 1);
        _ = this._contentGrid.Children.Add(this._textGrid);

        // Primary Text
        this._primaryText = new TextBlock {
            FontFamily = ThemeManager.FontFamily,
            FontSize = (double)TxtSz.m,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(ThemeManager.TextPrimaryColor),
            TextTrimming = ThemeManager.TextTrimmingMode
        };
        System.Windows.Controls.Grid.SetRow(this._primaryText, 0);
        _ = this._textGrid.Children.Add(this._primaryText);

        // Secondary Text
        this._secondaryText = new TextBlock {
            FontFamily = ThemeManager.FontFamily,
            FontSize = (double)TxtSz.s,
            Foreground = new SolidColorBrush(ThemeManager.TextSecondaryColor),
            Margin = new Thickness(0),
            TextTrimming = ThemeManager.TextTrimmingMode
        };
        System.Windows.Controls.Grid.SetRow(this._secondaryText, 1);
        _ = this._textGrid.Children.Add(this._secondaryText);

        // Pill Border
        this._pillBorder = new Border {
            Background = new SolidColorBrush(ThemeManager.PrimaryColor),
            BorderBrush = new SolidColorBrush(ThemeManager.SystemColor),
            BorderThickness = new Thickness((double)UiSz.ss),
            CornerRadius = new CornerRadius((double)UiSz.m),
            Padding = new Thickness((double)UiSz.l, (double)UiSz.s, (double)UiSz.l, (double)UiSz.s),
            Margin = new Thickness((double)UiSz.l, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        System.Windows.Controls.Grid.SetColumn(this._pillBorder, 2);
        _ = this._contentGrid.Children.Add(this._pillBorder);

        // Pill Text
        this._pillText = new TextBlock {
            FontFamily = ThemeManager.FontFamily,
            FontSize = (double)TxtSz.s,
            FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush(ThemeManager.TextSecondaryColor)
        };
        this._pillBorder.Child = this._pillText;
    }

    /// <summary>
    ///     Updates the control with data from an IPaletteListItem.
    /// </summary>
    public void UpdateFromDataContext(object dataContext) {
        if (dataContext is not IPaletteListItem item)
            return;

        // Update Primary Text
        this._primaryText.Text = item.TextPrimary ?? string.Empty;

        // Update Secondary Text and Visibility
        var hasSecondary = !string.IsNullOrWhiteSpace(item.TextSecondary);
        this._secondaryText.Text = hasSecondary ? item.TextSecondary : string.Empty;
        this._secondaryText.Visibility = hasSecondary ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        // Update Pill Text and Visibility
        var hasPill = !string.IsNullOrWhiteSpace(item.TextPill);
        this._pillText.Text = hasPill ? item.TextPill : string.Empty;
        this._pillBorder.Visibility = hasPill ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        // Update Icon and Visibility
        var hasIcon = item.Icon != null;
        this._iconImage.Source = hasIcon ? item.Icon : null;
        this._iconImage.Visibility = hasIcon ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        // Update Tooltip
        this.ToolTip = !string.IsNullOrWhiteSpace(item.TextInfo) ? item.TextInfo : null;

        // Update Opacity based on CanExecute
        this.Opacity = item.CanExecute ? ThemeManager.ItemOpacityEnabled : ThemeManager.ItemOpacityDisabled;
    }

    /// <summary>
    ///     Sets up bindings for the control (alternative to UpdateFromDataContext for binding scenarios).
    /// </summary>
    public void SetupBindings() {
        // Bind Primary Text
        var primaryBinding = new System.Windows.Data.Binding("TextPrimary") { Mode = BindingMode.OneWay };
        _ = this._primaryText.SetBinding(TextBlock.TextProperty, primaryBinding);

        // Bind Secondary Text
        var secondaryBinding = new System.Windows.Data.Binding("TextSecondary") { Mode = BindingMode.OneWay };
        _ = this._secondaryText.SetBinding(TextBlock.TextProperty, secondaryBinding);

        var secondaryVisibilityBinding = new System.Windows.Data.Binding("TextSecondary") {
            Mode = BindingMode.OneWay,
            Converter = new VisibilityConverter()
        };
        _ = this._secondaryText.SetBinding(VisibilityProperty, secondaryVisibilityBinding);

        // Bind Pill Text
        var pillTextBinding = new System.Windows.Data.Binding("TextPill") { Mode = BindingMode.OneWay };
        _ = this._pillText.SetBinding(TextBlock.TextProperty, pillTextBinding);

        var pillVisibilityBinding = new System.Windows.Data.Binding("TextPill") {
            Mode = BindingMode.OneWay,
            Converter = new VisibilityConverter()
        };
        _ = this._pillBorder.SetBinding(VisibilityProperty, pillVisibilityBinding);

        // Bind Icon
        var iconBinding = new System.Windows.Data.Binding("Icon") { Mode = BindingMode.OneWay };
        _ = this._iconImage.SetBinding(Image.SourceProperty, iconBinding);

        var iconVisibilityBinding = new System.Windows.Data.Binding("Icon") {
            Mode = BindingMode.OneWay,
            Converter = new VisibilityConverter()
        };
        _ = this._iconImage.SetBinding(VisibilityProperty, iconVisibilityBinding);

        // Bind Tooltip
        var tooltipBinding = new System.Windows.Data.Binding("TextInfo") { Mode = BindingMode.OneWay };
        _ = this.SetBinding(ToolTipProperty, tooltipBinding);

        // Bind Opacity based on CanExecute
        var opacityBinding = new System.Windows.Data.Binding("CanExecute") {
            Mode = BindingMode.OneWay,
            Converter = new CanExecuteToOpacityConverter()
        };
        _ = this.SetBinding(OpacityProperty, opacityBinding);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
        // Set up bindings once when DataContext is first set
        if (e.NewValue != null && e.OldValue == null)
            this.SetupBindings();
    }
}


