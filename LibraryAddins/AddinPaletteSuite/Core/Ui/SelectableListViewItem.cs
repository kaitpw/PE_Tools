using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;
using Visibility = System.Windows.Visibility;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     List item control for SelectableListView with XAML structure.
///     Implements shadcn-inspired styling using ThemeManager.
/// </summary>
public partial class SelectableListViewItem : Border {
    public SelectableListViewItem() {
        this.InitializeComponent();
        this.ApplyStyling();
        this.DataContextChanged += this.OnDataContextChanged;
    }

    private void ApplyStyling() {
        // Border styling
        this.CornerRadius = new CornerRadius((double)UiSz.l);
        _ = this.WithPadding(UiSz.ss, UiSz.s, UiSz.ll, UiSz.m);

        // Icon styling
        this.IconImage.Width = (double)UiSz.ll;
        this.IconImage.Height = (double)UiSz.ll;
        this.IconImage.Margin = new Thickness(0, 0, (double)UiSz.l, 0);
        this.IconImage.Opacity = ThemeManager.IconOpacity;

        // Text styling using ThemeManager helpers
        _ = ThemeManager.StyleTextBlock(this.PrimaryText);
        this.PrimaryText.FontWeight = FontWeights.Medium;

        _ = ThemeManager.StyleTextBlock(this.SecondaryText, TxtSz.s, false);
        _ = ThemeManager.StyleTextBlock(this.PillText, TxtSz.s, false);

        // Pill border styling
        var pillBackground = ThemeManager.TertiaryBg().Clone();
        pillBackground.Opacity = 0.5;
        this.PillBorder.Background = pillBackground;
        this.PillBorder.BorderBrush = ThemeManager.PrimaryHi();
        this.PillBorder.BorderThickness = new Thickness((double)UiSz.ss);
        this.PillBorder.CornerRadius = new CornerRadius((double)UiSz.m);
        _ = this.PillBorder.WithPadding(UiSz.m, 0, UiSz.m, UiSz.ss);
    }

    /// <summary>
    ///     Updates the control with data from an IPaletteListItem.
    /// </summary>
    public void UpdateFromDataContext(object dataContext) {
        if (dataContext is not IPaletteListItem item)
            return;

        // Update Primary Text
        this.PrimaryText.Text = item.TextPrimary ?? string.Empty;

        // Update Secondary Text and Visibility
        var hasSecondary = !string.IsNullOrWhiteSpace(item.TextSecondary);
        this.SecondaryText.Text = hasSecondary ? item.TextSecondary : string.Empty;
        this.SecondaryText.Visibility = hasSecondary ? Visibility.Visible : Visibility.Collapsed;

        // Update Pill Text and Visibility
        var hasPill = !string.IsNullOrWhiteSpace(item.TextPill);
        this.PillText.Text = hasPill ? item.TextPill : string.Empty;
        this.PillBorder.Visibility = hasPill ? Visibility.Visible : Visibility.Collapsed;

        // Update Icon and Visibility
        var hasIcon = item.Icon != null;
        this.IconImage.Source = hasIcon ? item.Icon : null;
        this.IconImage.Visibility = hasIcon ? Visibility.Visible : Visibility.Collapsed;

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
        var primaryBinding = new Binding("TextPrimary") { Mode = BindingMode.OneWay };
        _ = this.PrimaryText.SetBinding(TextBlock.TextProperty, primaryBinding);

        // Bind Secondary Text
        var secondaryBinding = new Binding("TextSecondary") { Mode = BindingMode.OneWay };
        _ = this.SecondaryText.SetBinding(TextBlock.TextProperty, secondaryBinding);

        var secondaryVisibilityBinding = new Binding("TextSecondary") {
            Mode = BindingMode.OneWay, Converter = new VisibilityConverter()
        };
        _ = this.SecondaryText.SetBinding(VisibilityProperty, secondaryVisibilityBinding);

        // Bind Pill Text
        var pillTextBinding = new Binding("TextPill") { Mode = BindingMode.OneWay };
        _ = this.PillText.SetBinding(TextBlock.TextProperty, pillTextBinding);

        var pillVisibilityBinding = new Binding("TextPill") {
            Mode = BindingMode.OneWay, Converter = new VisibilityConverter()
        };
        _ = this.PillBorder.SetBinding(VisibilityProperty, pillVisibilityBinding);

        // Bind Icon
        var iconBinding = new Binding("Icon") { Mode = BindingMode.OneWay };
        _ = this.IconImage.SetBinding(Image.SourceProperty, iconBinding);

        var iconVisibilityBinding = new Binding("Icon") {
            Mode = BindingMode.OneWay, Converter = new VisibilityConverter()
        };
        _ = this.IconImage.SetBinding(VisibilityProperty, iconVisibilityBinding);

        // Bind Tooltip
        var tooltipBinding = new Binding("TextInfo") { Mode = BindingMode.OneWay };
        _ = this.SetBinding(ToolTipProperty, tooltipBinding);

        // Bind Opacity based on CanExecute
        var opacityBinding = new Binding("CanExecute") {
            Mode = BindingMode.OneWay, Converter = new CanExecuteToOpacityConverter()
        };
        _ = this.SetBinding(OpacityProperty, opacityBinding);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
        // Set up bindings once when DataContext is first set
        if (e.NewValue != null && e.OldValue == null)
            this.SetupBindings();
    }
}