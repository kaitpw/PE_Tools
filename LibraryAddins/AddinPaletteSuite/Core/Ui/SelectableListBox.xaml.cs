using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace AddinPaletteSuite.Core.Ui;

public partial class SelectableListBox : UserControl {
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(SelectableListBox),
        new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem),
        typeof(object),
        typeof(SelectableListBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex),
        typeof(int),
        typeof(SelectableListBox),
        new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public SelectableListBox() {
        this.InitializeComponent();
        this.ApplyStyles();
        this.Loaded += this.SelectableListBox_Loaded;
    }

    private void ApplyStyles() {
        this.ApplyListBoxStyles();
    }

    private void ApplyListBoxStyles() {
        // Apply background to the main ListBox
        this.ItemListBox.Background = ThemeManager.BackgroundFillColorPrimaryBrush;
        this.ItemListBox.BorderThickness = ThemeManager.BorderThicknessNone;
    }

    private void SelectableListBox_Loaded(object sender, RoutedEventArgs e) {
        // Apply styles to DataTemplate items when they're loaded
        this.ItemListBox.ItemContainerGenerator.StatusChanged += this.ItemContainerGenerator_StatusChanged;
    }

    private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e) {
        if (this.ItemListBox.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated) {
            this.ApplyDataTemplateStyles();
        }
    }

    private void ApplyDataTemplateStyles() {
        foreach (var item in this.ItemListBox.Items) {
            var container = this.ItemListBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
            if (container != null) {
                this.ApplyItemContainerStyles(container);
                this.ApplyItemDataTemplateStyles(container);
            }
        }
    }

    private void ApplyItemContainerStyles(ListBoxItem container) {
        // Apply padding to the ListBoxItem
        container.Padding = ThemeManager.ItemPadding;
        container.BorderThickness = ThemeManager.BorderThicknessNone;
        container.HorizontalContentAlignment = HorizontalAlignment.Stretch;

        // Find the ItemBorder in the template and apply CornerRadius
        _ = container.ApplyTemplate();
        var itemBorder = this.FindChildByName<Border>(container, "ItemBorder");
        if (itemBorder != null) {
            itemBorder.CornerRadius = ThemeManager.CornerRadiusSmall;
        }

        // Apply hover and selection behaviors
        container.MouseEnter += (s, e) => {
            if (container.IsSelected) return;
            container.Background = ThemeManager.BackgroundFillColorSecondaryBrush;
        };

        container.MouseLeave += (s, e) => {
            if (container.IsSelected) return;
            container.Background = Brushes.Transparent;
        };

        // Update background when selection changes
        var updateBackground = new RoutedEventHandler((s, e) => container.Background = container.IsSelected
                ? ThemeManager.BackgroundFillColorSecondaryBrush
                : Brushes.Transparent);

        container.Selected += updateBackground;
        container.Unselected += updateBackground;
    }

    private void ApplyItemDataTemplateStyles(ListBoxItem container) {
        // Find named elements in the DataTemplate
        var contentPresenter = this.FindVisualChild<ContentPresenter>(container);
        if (contentPresenter == null) return;

        // Apply the DataTemplate so we can find its children
        _ = contentPresenter.ApplyTemplate();

        // Find and style elements by name
        var primaryTextBlock = this.FindChildByName<TextBlock>(contentPresenter, "PrimaryTextBlock");
        if (primaryTextBlock != null) {
            primaryTextBlock.FontFamily = ThemeManager.FontFamily;
            primaryTextBlock.FontSize = ThemeManager.FontSizeLarge;
            primaryTextBlock.FontWeight = ThemeManager.FontWeightSemiBold;
            primaryTextBlock.Foreground = ThemeManager.TextFillColorPrimaryBrush;
        }

        var secondaryTextBlock = this.FindChildByName<TextBlock>(contentPresenter, "SecondaryTextBlock");
        if (secondaryTextBlock != null) {
            secondaryTextBlock.FontFamily = ThemeManager.FontFamily;
            secondaryTextBlock.FontSize = ThemeManager.FontSizeSmall;
            secondaryTextBlock.Foreground = ThemeManager.TextFillColorSecondaryBrush;
            secondaryTextBlock.Margin = new Thickness(0);
        }

        var pillBorder = this.FindChildByName<Border>(contentPresenter, "PillBorder");
        if (pillBorder != null) {
            pillBorder.Background = ThemeManager.BackgroundFillColorTertiaryBrush;
            pillBorder.BorderBrush = ThemeManager.ControlStrokeColorDefaultBrush;
            pillBorder.BorderThickness = ThemeManager.BorderThicknessThin;
            pillBorder.CornerRadius = ThemeManager.PillCornerRadius;
            pillBorder.Padding = ThemeManager.PillPadding;
            pillBorder.Margin = ThemeManager.PillMargin;
        }

        var pillTextBlock = this.FindChildByName<TextBlock>(contentPresenter, "PillTextBlock");
        if (pillTextBlock != null) {
            pillTextBlock.FontFamily = ThemeManager.FontFamily;
            pillTextBlock.FontSize = ThemeManager.FontSizeSmall;
            pillTextBlock.FontWeight = ThemeManager.FontWeightMedium;
            pillTextBlock.Foreground = ThemeManager.TextFillColorSecondaryBrush;
        }

        var itemIcon = this.FindChildByName<Image>(contentPresenter, "ItemIcon");
        if (itemIcon != null) {
            itemIcon.Width = ThemeManager.IconSize;
            itemIcon.Height = ThemeManager.IconSize;
            itemIcon.Margin = ThemeManager.IconMargin;
            itemIcon.Opacity = ThemeManager.IconOpacity;
        }
    }

    private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) {
                return typedChild;
            }
            var result = this.FindVisualChild<T>(child);
            if (result != null) {
                return result;
            }
        }
        return null;
    }

    private T FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild && typedChild.Name == name) {
                return typedChild;
            }
            var result = this.FindChildByName<T>(child, name);
            if (result != null) {
                return result;
            }
        }
        return null;
    }

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

    public ItemContainerGenerator ItemContainerGenerator => this.ItemListBox.ItemContainerGenerator;

    public event SelectionChangedEventHandler SelectionChanged;
    public event MouseButtonEventHandler ItemMouseLeftButtonUp;

    public void ScrollIntoView(object item) => this.ItemListBox?.ScrollIntoView(item);

    private void ItemListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        this.SelectionChanged?.Invoke(this, e);
    }

    private void ItemListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        this.ItemMouseLeftButtonUp?.Invoke(this, e);
    }
}