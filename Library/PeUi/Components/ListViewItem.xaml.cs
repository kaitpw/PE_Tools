using PeUi.Core;
using PeUi.Core.Converters;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Binding = System.Windows.Data.Binding;
using Image = System.Windows.Controls.Image;
using TextBlock = System.Windows.Controls.TextBlock;
using Visibility = System.Windows.Visibility;

namespace PeUi.Components;

/// <summary>
///     List item control for ListView with XAML structure.
///     Implements shadcn-inspired styling using ThemeManager.
/// </summary>
public partial class ListViewItem : Border {
    public ListViewItem() {
        this.InitializeComponent();
        this.ApplyStyling();
        this.DataContextChanged += this.OnDataContextChanged;
    }

    private void ApplyStyling() {
        // Border styling (layout only, colors from XAML DynamicResources)
        this.CornerRadius = new CornerRadius((double)UiSz.l);
        new BorderSpec()
            .Padding(UiSz.ss, UiSz.s, UiSz.ll, UiSz.m)
            .ApplyToBorder(this);

        // Icon styling
        this.IconImage.Width = (double)UiSz.ll;
        this.IconImage.Height = (double)UiSz.ll;
        this.IconImage.Margin = new Thickness(0, 0, (double)UiSz.l, 0);
        this.IconImage.Opacity = ThemeManager.IconOpacity;

        // Text styling - apply typography styles to override WPF.UI defaults
        // this.PrimaryText.Style = ThemeManager.GetTypographyStyle(FontTypography.BodyStrong);
        // this.SecondaryText.Style = ThemeManager.GetTypographyStyle(FontTypography.Caption);

        // Pill styling is now handled by the Pill component itself
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
        this.PillBorder.Text = hasPill ? item.TextPill : string.Empty;
        this.PillBorder.Visibility = hasPill ? Visibility.Visible : Visibility.Collapsed;

        // Update Icon and Visibility
        var hasIcon = item.Icon != null;
        this.IconImage.Source = hasIcon ? item.Icon : null;
        this.IconImage.Visibility = hasIcon ? Visibility.Visible : Visibility.Collapsed;

        // Update Color Indicator
        if (item.ItemColor.HasValue) {
            this.ColorIndicator.Background = new SolidColorBrush(item.ItemColor.Value);
            this.ColorIndicator.Visibility = Visibility.Visible;
        } else
            this.ColorIndicator.Visibility = Visibility.Collapsed;

        // Update Opacity based on actions (compute executability from actions)
        var canExecute = this.ComputeCanExecute(item);
        this.Opacity = canExecute ? 1 : ThemeManager.DisabledOpacity;
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
        _ = this.PillBorder.SetBinding(Pill.TextProperty, pillTextBinding);

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

        // Bind Color Indicator Background and Visibility
        var colorBackgroundBinding = new Binding("ItemColor") {
            Mode = BindingMode.OneWay, Converter = new ColorToBrushConverter()
        };
        _ = this.ColorIndicator.SetBinding(BackgroundProperty, colorBackgroundBinding);

        var colorVisibilityBinding = new Binding("ItemColor") {
            Mode = BindingMode.OneWay, Converter = new NullableColorToVisibilityConverter()
        };
        _ = this.ColorIndicator.SetBinding(VisibilityProperty, colorVisibilityBinding);

        // Tooltip disabled - no hover tooltips

        // Compute opacity from actions (no binding needed since CanExecute doesn't change after palette opens)
        var item = this.DataContext as IPaletteListItem;
        if (item != null) {
            var canExecute = this.ComputeCanExecute(item);
            this.Opacity = canExecute ? 1 : ThemeManager.DisabledOpacity;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
        // Set up bindings once when DataContext is first set
        if (e.NewValue != null && e.OldValue == null)
            this.SetupBindings();
    }

    /// <summary>
    ///     Computes whether an item can be executed by checking available actions
    /// </summary>
    private bool ComputeCanExecute(IPaletteListItem item) {
        var actionBinding = this.FindActionBinding();
        if (actionBinding == null) return true; // Default to executable if no actions found

        // Use reflection to call HasAvailableActions method
        var hasAvailableActionsMethod = actionBinding.GetType().GetMethod("HasAvailableActions");
        if (hasAvailableActionsMethod != null) {
            var result = hasAvailableActionsMethod.Invoke(actionBinding, new object[] { item });
            return result is bool canExecute && canExecute;
        }

        return true; // Default to executable if method not found
    }

    /// <summary>
    ///     Finds the ActionBinding by walking up the visual tree to find SelectablePalette
    /// </summary>
    private object FindActionBinding() {
        var current = this.Parent;
        while (current != null) {
            var actionBinding = Palette.GetActionBinding(current);
            if (actionBinding != null) return actionBinding;

            current = current is FrameworkElement fe ? fe.Parent : null;
        }

        return null;
    }
}