using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AddinPaletteSuite.Core.Ui;

public partial class SelectableListView : UserControl {
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(SelectableListView),
        new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem),
        typeof(object),
        typeof(SelectableListView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex),
        typeof(int),
        typeof(SelectableListView),
        new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public SelectableListView() {
        this.InitializeComponent();
        this.ItemListView.ItemTemplate = new DataTemplate {
            VisualTree = new FrameworkElementFactory(typeof(SelectableListViewItem))
        };
        this.InitializeThemeBrushes();
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

    public ItemContainerGenerator ItemContainerGenerator => this.ItemListView.ItemContainerGenerator;

    private void InitializeThemeBrushes() {
        // Set theme brushes for hot-reload support (methods instead of static properties)
        this.Resources["Highlight"] = ThemeManager.SecondaryHi();
        this.Resources["SeparatorBrush"] = ThemeManager.TertiaryBg();
    }

    public event SelectionChangedEventHandler SelectionChanged;
    public event MouseButtonEventHandler ItemMouseLeftButtonUp;

    public void ScrollIntoView(object item) => this.ItemListView?.ScrollIntoView(item);

    private void ItemListView_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        this.SelectionChanged?.Invoke(this, e);

    private void ItemListView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        this.ItemMouseLeftButtonUp?.Invoke(this, e);
}