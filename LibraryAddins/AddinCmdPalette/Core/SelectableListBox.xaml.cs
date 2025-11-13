using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfUiDynamicScrollBar = Wpf.Ui.Controls.DynamicScrollBar;

namespace AddinCmdPalette.Core;


public class SelectableListBox : UserControl {
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(System.Collections.IEnumerable),
        typeof(SelectableListBox),
        new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem),
        typeof(object),
        typeof(SelectableListBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex),
        typeof(int),
        typeof(SelectableListBox),
        new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedIndexChanged));

    private ListBox _listBox;
    private bool _isInitialized;
    private static readonly VisibilityConverter _visibilityConverter = VisibilityConverter.Instance;

    public SelectableListBox() => this.InitializeControls();

    private void InitializeControls() {
        if (this._isInitialized) return;

        // Create ListBox
        this._listBox = new ListBox {
            Background = (Brush)this.TryFindResource("BackgroundFillColorPrimaryBrush") ?? Brushes.Black,
            BorderThickness = new Thickness(0)
        };

        // Set up ScrollViewer template with WPF.UI DynamicScrollBar
        var scrollViewerStyle = new Style(typeof(ScrollViewer));
        scrollViewerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, Brushes.Transparent));
        scrollViewerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty, new Thickness(0)));

        var scrollViewerTemplate = new ControlTemplate(typeof(ScrollViewer));
        var scrollViewerGrid = new FrameworkElementFactory(typeof(System.Windows.Controls.Grid));

        var columnDef1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        columnDef1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var columnDef2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        columnDef2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);

        scrollViewerGrid.AppendChild(columnDef1);
        scrollViewerGrid.AppendChild(columnDef2);

        var scrollContentPresenter = new FrameworkElementFactory(typeof(ScrollContentPresenter));
        scrollContentPresenter.SetBinding(ScrollContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        scrollContentPresenter.SetBinding(ScrollContentPresenter.ContentTemplateProperty, new System.Windows.Data.Binding("ContentTemplate") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        scrollContentPresenter.SetBinding(ScrollContentPresenter.CanContentScrollProperty, new System.Windows.Data.Binding("CanContentScroll") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        scrollContentPresenter.SetValue(System.Windows.Controls.Grid.ColumnProperty, 0);
        scrollViewerGrid.AppendChild(scrollContentPresenter);

        var dynamicScrollBar = new FrameworkElementFactory(typeof(WpfUiDynamicScrollBar));
        dynamicScrollBar.SetValue(FrameworkElement.NameProperty, "PART_VerticalScrollBar");
        dynamicScrollBar.SetValue(WpfUiDynamicScrollBar.OrientationProperty, Orientation.Vertical);
        dynamicScrollBar.SetBinding(WpfUiDynamicScrollBar.ViewportSizeProperty, new System.Windows.Data.Binding("ViewportHeight") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        dynamicScrollBar.SetBinding(WpfUiDynamicScrollBar.MaximumProperty, new System.Windows.Data.Binding("ScrollableHeight") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        dynamicScrollBar.SetBinding(WpfUiDynamicScrollBar.ValueProperty, new System.Windows.Data.Binding("VerticalOffset") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Mode = BindingMode.OneWay });
        dynamicScrollBar.SetBinding(UIElement.VisibilityProperty, new System.Windows.Data.Binding("ComputedVerticalScrollBarVisibility") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        dynamicScrollBar.SetValue(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Arrow);
        dynamicScrollBar.SetValue(System.Windows.Controls.Grid.ColumnProperty, 1);
        scrollViewerGrid.AppendChild(dynamicScrollBar);

        scrollViewerTemplate.VisualTree = scrollViewerGrid;
        scrollViewerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.TemplateProperty, scrollViewerTemplate));
        this._listBox.Resources.Add(typeof(ScrollViewer), scrollViewerStyle);

        // Set up ItemContainerStyle
        var itemContainerStyle = new Style(typeof(ListBoxItem));
        itemContainerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, Brushes.Transparent));
        itemContainerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty, (Thickness)(this.TryFindResource("PaletteItemPadding") ?? new Thickness(10, 5, 10, 5))));
        itemContainerStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0)));
        itemContainerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderThicknessProperty, new Thickness(0)));
        itemContainerStyle.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        itemContainerStyle.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));

        var mouseOverTrigger = new Trigger {
            Property = UIElement.IsMouseOverProperty,
            Value = true
        };
        mouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, (Brush)this.TryFindResource("BackgroundFillColorSecondaryBrush") ?? Brushes.DarkGray));
        itemContainerStyle.Triggers.Add(mouseOverTrigger);

        var selectedTrigger = new Trigger {
            Property = Selector.IsSelectedProperty,
            Value = true
        };
        selectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, (Brush)this.TryFindResource("BackgroundFillColorSecondaryBrush") ?? Brushes.DarkGray));
        itemContainerStyle.Triggers.Add(selectedTrigger);

        this._listBox.ItemContainerStyle = itemContainerStyle;

        // Set up ItemTemplate
        var itemTemplate = new DataTemplate();

        var itemGrid = new FrameworkElementFactory(typeof(System.Windows.Controls.Grid));

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
        iconImage.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding("Icon"));
        iconImage.SetValue(FrameworkElement.WidthProperty, 16.0);
        iconImage.SetValue(FrameworkElement.HeightProperty, 16.0);
        iconImage.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
        iconImage.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        iconImage.SetValue(UIElement.OpacityProperty, 0.8);
        var iconVisibilityBinding = new System.Windows.Data.Binding("Icon") { Converter = _visibilityConverter };
        iconImage.SetBinding(UIElement.VisibilityProperty, iconVisibilityBinding);
        iconImage.SetValue(System.Windows.Controls.Grid.ColumnProperty, 0);
        itemGrid.AppendChild(iconImage);

        // Text StackPanel
        var textStackPanel = new FrameworkElementFactory(typeof(StackPanel));
        textStackPanel.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
        textStackPanel.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        textStackPanel.SetValue(System.Windows.Controls.Grid.ColumnProperty, 1);

        var primaryTextBlock = new FrameworkElementFactory(typeof(TextBlock));
        primaryTextBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("PrimaryText"));
        primaryTextBlock.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI Variable"));
        primaryTextBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        primaryTextBlock.SetValue(TextBlock.FontSizeProperty, (double)(this.TryFindResource("PaletteFontSizeLarge") ?? 13.0));
        primaryTextBlock.SetValue(TextBlock.LineHeightProperty, 18.0);
        primaryTextBlock.SetValue(TextBlock.ForegroundProperty, (Brush)this.TryFindResource("TextFillColorPrimaryBrush") ?? Brushes.White);
        textStackPanel.AppendChild(primaryTextBlock);

        var secondaryTextBlock = new FrameworkElementFactory(typeof(TextBlock));
        secondaryTextBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("SecondaryText"));
        secondaryTextBlock.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI Variable"));
        secondaryTextBlock.SetValue(TextBlock.FontSizeProperty, (double)(this.TryFindResource("PaletteFontSizeSmall") ?? 10.0));
        secondaryTextBlock.SetValue(FrameworkElement.MarginProperty, new Thickness(0));
        secondaryTextBlock.SetValue(TextBlock.LineHeightProperty, 14.0);
        secondaryTextBlock.SetValue(TextBlock.ForegroundProperty, (Brush)this.TryFindResource("TextFillColorSecondaryBrush") ?? Brushes.Gray);
        var secondaryVisibilityBinding = new System.Windows.Data.Binding("SecondaryText") { Converter = _visibilityConverter };
        secondaryTextBlock.SetBinding(UIElement.VisibilityProperty, secondaryVisibilityBinding);
        textStackPanel.AppendChild(secondaryTextBlock);

        itemGrid.AppendChild(textStackPanel);

        // Pill Border - shadcn style
        var pillBorder = new FrameworkElementFactory(typeof(Border));
        pillBorder.SetValue(System.Windows.Controls.Control.BackgroundProperty, (Brush)this.TryFindResource("PillBackgroundBrush") ?? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27272A")));
        pillBorder.SetValue(Border.BorderBrushProperty, (Brush)this.TryFindResource("PillBorderBrush") ?? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3F3F46")));
        pillBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        pillBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        pillBorder.SetValue(System.Windows.Controls.Control.PaddingProperty, (Thickness)(this.TryFindResource("PalettePaddingSmall") ?? new Thickness(4, 2, 4, 2)));
        pillBorder.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
        pillBorder.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        pillBorder.SetValue(System.Windows.Controls.Grid.ColumnProperty, 2);
        var pillVisibilityBinding = new System.Windows.Data.Binding("PillText") { Converter = _visibilityConverter };
        pillBorder.SetBinding(UIElement.VisibilityProperty, pillVisibilityBinding);

        var pillTextBlock = new FrameworkElementFactory(typeof(TextBlock));
        pillTextBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("PillText"));
        pillTextBlock.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI Variable"));
        pillTextBlock.SetValue(TextBlock.FontSizeProperty, (double)(this.TryFindResource("PaletteFontSizeSmall") ?? 10.0));
        pillTextBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
        pillTextBlock.SetValue(TextBlock.ForegroundProperty, (Brush)this.TryFindResource("PillTextBrush") ?? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A1A1AA")));
        pillBorder.AppendChild(pillTextBlock);

        itemGrid.AppendChild(pillBorder);

        // Set ToolTip binding on Grid
        var tooltipBinding = new System.Windows.Data.Binding("TooltipText");
        itemGrid.SetBinding(FrameworkElement.ToolTipProperty, tooltipBinding);

        itemTemplate.VisualTree = itemGrid;
        this._listBox.ItemTemplate = itemTemplate;

        // Bind properties
        _ = this._listBox.SetBinding(ItemsControl.ItemsSourceProperty, new System.Windows.Data.Binding(nameof(this.ItemsSource)) { Source = this });
        _ = this._listBox.SetBinding(Selector.SelectedItemProperty, new System.Windows.Data.Binding(nameof(this.SelectedItem)) { Source = this, Mode = BindingMode.TwoWay });
        _ = this._listBox.SetBinding(Selector.SelectedIndexProperty, new System.Windows.Data.Binding(nameof(this.SelectedIndex)) { Source = this, Mode = BindingMode.TwoWay });

        // Wire up events
        this._listBox.SelectionChanged += (s, e) => this.SelectionChanged?.Invoke(this, e);
        this._listBox.MouseLeftButtonUp += (s, e) => this.ItemMouseLeftButtonUp?.Invoke(this, e);

        this.Content = this._listBox;
        this._isInitialized = true;
    }

    public System.Collections.IEnumerable ItemsSource {
        get => (System.Collections.IEnumerable)this.GetValue(ItemsSourceProperty);
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

    public event SelectionChangedEventHandler SelectionChanged;
    public event MouseButtonEventHandler ItemMouseLeftButtonUp;

    public void ScrollIntoView(object item) => this._listBox?.ScrollIntoView(item);

    public ItemContainerGenerator ItemContainerGenerator => this._listBox.ItemContainerGenerator;

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

