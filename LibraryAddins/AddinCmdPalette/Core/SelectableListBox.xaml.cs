using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Binding = System.Windows.Data.Binding;
using Grid = System.Windows.Controls.Grid;
using WpfUiDynamicScrollBar = Wpf.Ui.Controls.DynamicScrollBar;

namespace AddinCmdPalette.Core;

public class SelectableListBox : UserControl {
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(SelectableListBox),
        new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem),
        typeof(object),
        typeof(SelectableListBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnSelectedItemChanged));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex),
        typeof(int),
        typeof(SelectableListBox),
        new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnSelectedIndexChanged));

    private static readonly VisibilityConverter _visibilityConverter = VisibilityConverter.Instance;
    private bool _isInitialized;

    private ListBox _listBox;

    public SelectableListBox() => this.InitializeControls();

    public IEnumerable ItemsSource {
        get => (IEnumerable)this.GetValue(ItemsSourceProperty);
        set => this.SetValue(ItemsSourceProperty, value);
    }

    public object SelectedItem {
        get => this.GetValue(SelectedItemProperty);
        set => this.SetValue(SelectedItemProperty, value);
    }

    public int SelectedIndex {
        get => (int)this.GetValue(SelectedIndexProperty);
        set => this.SetValue(SelectedIndexProperty, value);
    }

    public ItemContainerGenerator ItemContainerGenerator => this._listBox.ItemContainerGenerator;

    private void InitializeControls() {
        if (this._isInitialized) return;

        // Create ListBox
        this._listBox = new ListBox {
            Background = (Brush)this.TryFindResource("BackgroundFillColorPrimaryBrush") ?? Brushes.Black,
            BorderThickness = new Thickness(0)
        };

        // Set up ScrollViewer template with WPF.UI DynamicScrollBar
        var scrollViewerStyle = new Style(typeof(ScrollViewer));
        scrollViewerStyle.Setters.Add(new Setter(BackgroundProperty, Brushes.Transparent));
        scrollViewerStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(0)));

        var scrollViewerTemplate = new ControlTemplate(typeof(ScrollViewer));
        var scrollViewerGrid = new FrameworkElementFactory(typeof(Grid));

        var columnDef1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        columnDef1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var columnDef2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        columnDef2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);

        scrollViewerGrid.AppendChild(columnDef1);
        scrollViewerGrid.AppendChild(columnDef2);

        var scrollContentPresenter = new FrameworkElementFactory(typeof(ScrollContentPresenter));
        scrollContentPresenter.SetBinding(ScrollContentPresenter.ContentProperty,
            new Binding("Content") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        scrollContentPresenter.SetBinding(ScrollContentPresenter.ContentTemplateProperty,
            new Binding("ContentTemplate") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        scrollContentPresenter.SetBinding(ScrollContentPresenter.CanContentScrollProperty,
            new Binding("CanContentScroll") {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
        scrollContentPresenter.SetValue(Grid.ColumnProperty, 0);
        scrollViewerGrid.AppendChild(scrollContentPresenter);

        var dynamicScrollBar = new FrameworkElementFactory(typeof(WpfUiDynamicScrollBar));
        dynamicScrollBar.SetValue(NameProperty, "PART_VerticalScrollBar");
        dynamicScrollBar.SetValue(WpfUiDynamicScrollBar.OrientationProperty, Orientation.Vertical);
        dynamicScrollBar.SetBinding(WpfUiDynamicScrollBar.ViewportSizeProperty,
            new Binding("ViewportHeight") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        dynamicScrollBar.SetBinding(WpfUiDynamicScrollBar.MaximumProperty,
            new Binding("ScrollableHeight") {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
        dynamicScrollBar.SetBinding(WpfUiDynamicScrollBar.ValueProperty,
            new Binding("VerticalOffset") {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Mode = BindingMode.OneWay
            });
        dynamicScrollBar.SetBinding(VisibilityProperty,
            new Binding("ComputedVerticalScrollBarVisibility") {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
        dynamicScrollBar.SetValue(CursorProperty, Cursors.Arrow);
        dynamicScrollBar.SetValue(Grid.ColumnProperty, 1);
        scrollViewerGrid.AppendChild(dynamicScrollBar);

        scrollViewerTemplate.VisualTree = scrollViewerGrid;
        scrollViewerStyle.Setters.Add(new Setter(TemplateProperty, scrollViewerTemplate));
        this._listBox.Resources.Add(typeof(ScrollViewer), scrollViewerStyle);

        // Set up ItemContainerStyle
        var itemContainerStyle = new Style(typeof(ListBoxItem));
        itemContainerStyle.Setters.Add(new Setter(BackgroundProperty, Brushes.Transparent));
        itemContainerStyle.Setters.Add(new Setter(PaddingProperty,
            (Thickness)(this.TryFindResource("PaletteItemPadding") ?? new Thickness(10, 5, 10, 5))));
        itemContainerStyle.Setters.Add(new Setter(MarginProperty, new Thickness(0)));
        itemContainerStyle.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0)));
        itemContainerStyle.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        itemContainerStyle.Setters.Add(new Setter(FocusVisualStyleProperty, null));

        var mouseOverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
        mouseOverTrigger.Setters.Add(new Setter(BackgroundProperty,
            (Brush)this.TryFindResource("BackgroundFillColorSecondaryBrush") ?? Brushes.DarkGray));
        itemContainerStyle.Triggers.Add(mouseOverTrigger);

        var selectedTrigger = new Trigger { Property = Selector.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(BackgroundProperty,
            (Brush)this.TryFindResource("BackgroundFillColorSecondaryBrush") ?? Brushes.DarkGray));
        itemContainerStyle.Triggers.Add(selectedTrigger);

        this._listBox.ItemContainerStyle = itemContainerStyle;

        // Set up ItemTemplate
        var itemTemplate = new DataTemplate();

        var itemGrid = new FrameworkElementFactory(typeof(Grid));

        var gridColumn1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        gridColumn1.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        var gridColumn2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        gridColumn2.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var gridColumn3 = new FrameworkElementFactory(typeof(ColumnDefinition));
        gridColumn3.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);

        itemGrid.AppendChild(gridColumn1);
        itemGrid.AppendChild(gridColumn2);
        itemGrid.AppendChild(gridColumn3);

        // Icon
        var iconImage = new FrameworkElementFactory(typeof(Image));
        iconImage.SetBinding(Image.SourceProperty, new Binding("Icon"));
        iconImage.SetValue(WidthProperty, 16.0);
        iconImage.SetValue(HeightProperty, 16.0);
        iconImage.SetValue(MarginProperty, new Thickness(0, 0, 10, 0));
        iconImage.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        iconImage.SetValue(OpacityProperty, 0.8);
        var iconVisibilityBinding = new Binding("Icon") { Converter = _visibilityConverter };
        iconImage.SetBinding(VisibilityProperty, iconVisibilityBinding);
        iconImage.SetValue(Grid.ColumnProperty, 0);
        itemGrid.AppendChild(iconImage);

        // Text StackPanel
        var textStackPanel = new FrameworkElementFactory(typeof(StackPanel));
        textStackPanel.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
        textStackPanel.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        textStackPanel.SetValue(Grid.ColumnProperty, 1);

        var primaryTextBlock = new FrameworkElementFactory(typeof(TextBlock));
        primaryTextBlock.SetBinding(TextBlock.TextProperty, new Binding("PrimaryText"));
        primaryTextBlock.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI Variable"));
        primaryTextBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        primaryTextBlock.SetValue(TextBlock.FontSizeProperty,
            (double)(this.TryFindResource("PaletteFontSizeLarge") ?? 13.0));
        primaryTextBlock.SetValue(TextBlock.LineHeightProperty, 18.0);
        primaryTextBlock.SetValue(TextBlock.ForegroundProperty,
            (Brush)this.TryFindResource("TextFillColorPrimaryBrush") ?? Brushes.White);
        textStackPanel.AppendChild(primaryTextBlock);

        var secondaryTextBlock = new FrameworkElementFactory(typeof(TextBlock));
        secondaryTextBlock.SetBinding(TextBlock.TextProperty, new Binding("SecondaryText"));
        secondaryTextBlock.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI Variable"));
        secondaryTextBlock.SetValue(TextBlock.FontSizeProperty,
            (double)(this.TryFindResource("PaletteFontSizeSmall") ?? 10.0));
        secondaryTextBlock.SetValue(MarginProperty, new Thickness(0));
        secondaryTextBlock.SetValue(TextBlock.LineHeightProperty, 14.0);
        secondaryTextBlock.SetValue(TextBlock.ForegroundProperty,
            (Brush)this.TryFindResource("TextFillColorSecondaryBrush") ?? Brushes.Gray);
        var secondaryVisibilityBinding = new Binding("SecondaryText") { Converter = _visibilityConverter };
        secondaryTextBlock.SetBinding(VisibilityProperty, secondaryVisibilityBinding);
        textStackPanel.AppendChild(secondaryTextBlock);

        itemGrid.AppendChild(textStackPanel);

        // Pill Border - shadcn style
        var pillBorder = new FrameworkElementFactory(typeof(Border));
        pillBorder.SetValue(BackgroundProperty,
            (Brush)this.TryFindResource("PillBackgroundBrush") ??
            new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#27272A")));
        pillBorder.SetValue(Border.BorderBrushProperty,
            (Brush)this.TryFindResource("PillBorderBrush") ??
            new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#3F3F46")));
        pillBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        pillBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        pillBorder.SetValue(PaddingProperty,
            (Thickness)(this.TryFindResource("PalettePaddingSmall") ?? new Thickness(4, 2, 4, 2)));
        pillBorder.SetValue(MarginProperty, new Thickness(8, 0, 0, 0));
        pillBorder.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        pillBorder.SetValue(Grid.ColumnProperty, 2);
        var pillVisibilityBinding = new Binding("PillText") { Converter = _visibilityConverter };
        pillBorder.SetBinding(VisibilityProperty, pillVisibilityBinding);

        var pillTextBlock = new FrameworkElementFactory(typeof(TextBlock));
        pillTextBlock.SetBinding(TextBlock.TextProperty, new Binding("PillText"));
        pillTextBlock.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI Variable"));
        pillTextBlock.SetValue(TextBlock.FontSizeProperty,
            (double)(this.TryFindResource("PaletteFontSizeSmall") ?? 10.0));
        pillTextBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
        pillTextBlock.SetValue(TextBlock.ForegroundProperty,
            (Brush)this.TryFindResource("PillTextBrush") ??
            new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#A1A1AA")));
        pillBorder.AppendChild(pillTextBlock);

        itemGrid.AppendChild(pillBorder);

        // Set ToolTip binding on Grid
        var tooltipBinding = new Binding("TooltipText");
        itemGrid.SetBinding(ToolTipProperty, tooltipBinding);

        itemTemplate.VisualTree = itemGrid;
        this._listBox.ItemTemplate = itemTemplate;

        // Bind properties
        _ = this._listBox.SetBinding(ItemsControl.ItemsSourceProperty,
            new Binding(nameof(this.ItemsSource)) { Source = this });
        _ = this._listBox.SetBinding(Selector.SelectedItemProperty,
            new Binding(nameof(this.SelectedItem)) { Source = this, Mode = BindingMode.TwoWay });
        _ = this._listBox.SetBinding(Selector.SelectedIndexProperty,
            new Binding(nameof(this.SelectedIndex)) { Source = this, Mode = BindingMode.TwoWay });

        // Wire up events
        this._listBox.SelectionChanged += (s, e) => this.SelectionChanged?.Invoke(this, e);
        this._listBox.MouseLeftButtonUp += (s, e) => this.ItemMouseLeftButtonUp?.Invoke(this, e);

        this.Content = this._listBox;
        this._isInitialized = true;
    }

    public event SelectionChangedEventHandler SelectionChanged;
    public event MouseButtonEventHandler ItemMouseLeftButtonUp;

    public void ScrollIntoView(object item) => this._listBox?.ScrollIntoView(item);

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        // Binding handles this
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        // Binding handles this
    }

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        // Binding handles this
    }
}