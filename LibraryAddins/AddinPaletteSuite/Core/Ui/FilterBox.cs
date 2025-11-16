#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using Binding = System.Windows.Data.Binding;
using ListView = Wpf.Ui.Controls.ListView;
using ListViewItem = Wpf.Ui.Controls.ListViewItem;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Non-generic base class for FilterBox.xaml
///     This matches the XAML x:Class declaration and provides access to XAML-defined controls
/// </summary>
public partial class FilterBox : UserControl, IPopoverExit {
    private bool _isExpanded;

    protected FilterBox() => this.InitializeComponent();

    public UIElement? ReturnFocusTarget { get; set; }
    public event EventHandler? ExitRequested;

    public void RequestExit() {
        this.ExitRequested?.Invoke(this, EventArgs.Empty);
        _ = this.ReturnFocusTarget?.Focus();
    }

    /// <summary>
    ///     Focuses the FilterBox by expanding and focusing the AutoSuggestBox
    /// </summary>
    public new void Focus() => this.Expand();

    protected void IconBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _ = this.FilterAutoSuggestBox.Focus();

    protected void FilterAutoSuggestBox_GotFocus(object sender, RoutedEventArgs e) {
        Debug.WriteLine($"[FilterBox] FilterAutoSuggestBox_GotFocus - Expanded: {this._isExpanded}");
        if (!this._isExpanded) this.Expand();
    }

    protected void FilterAutoSuggestBox_LostFocus(object sender, RoutedEventArgs e) {
        var newFocus = Keyboard.FocusedElement;
        var isListView = newFocus is ListView;
        var isListViewItem = newFocus is ListViewItem;

        if (isListView || isListViewItem) return;

        // Always collapse when unfocused (but not when navigating to suggestions)
        this.Collapse();
    }

    protected void Expand() {
        if (this._isExpanded) return;
        this._isExpanded = true;

        var storyboard = this.FindResource("ExpandStoryboard") as Storyboard;
        storyboard?.Begin();

        // Focus the AutoSuggestBox after expansion starts
        _ = this.Dispatcher.BeginInvoke(new Action(() => _ = this.FilterAutoSuggestBox.Focus()),
            DispatcherPriority.Input);
    }

    protected void Collapse() {
        if (!this._isExpanded) return;
        this._isExpanded = false;

        (this.FindResource("CollapseStoryboard") as Storyboard)?.Begin();
    }
}

/// <summary>
///     Generic FilterBox implementation with typed ViewModel support
///     Provides filtering functionality with AutoSuggestBox
/// </summary>
public class FilterBox<TViewModel> : FilterBox where TViewModel : class {
    private readonly TViewModel _viewModel;

    public FilterBox(TViewModel viewModel) {
        this._viewModel = viewModel;

        this.ApplyStyles();

        this.FilterAutoSuggestBox.SuggestionChosen += this.FilterAutoSuggestBox_SuggestionChosen;
        this.FilterAutoSuggestBox.PreviewKeyDown += this.FilterAutoSuggestBox_PreviewKeyDown;
    }

    /// <summary>
    /// This event fires EVERY time a list item is focused by the keyboard. update view model here.
    /// </summary>
    private void FilterAutoSuggestBox_SuggestionChosen(object sender, AutoSuggestBoxSuggestionChosenEventArgs e) {
        this.UpdateSelectedFilterValue(e.SelectedItem.ToString());
        e.Handled = true;
    }

    /// <summary>
    /// Handle escaping and unfocusing the FilterBox here.
    /// </summary>
    private void FilterAutoSuggestBox_PreviewKeyDown(object sender, KeyEventArgs e) {
        if (e.Key is Key.Tab or Key.Escape) {
            e.Handled = true;
            this.RequestExit();
        } else if (e.Key == Key.Enter) {
            e.Handled = true;
            this.RequestExit();
        }
    }

    private void ApplyStyles() {
        // Apply corner radius
        var topRadius = ThemeManager.Radius.TopLeft;
        this.FilterBorder.CornerRadius = new CornerRadius(topRadius, topRadius, 0, 0);
        // Remove padding to match search box height - padding is handled by SearchBoxBorder
        _ = this.FilterBorder
            .WithSpacing(0, 0)
            .WithPadding(0, 0, 0, 0);

        // Apply typography style
        ThemeManager.ApplyTypographyStyle(this.FilterAutoSuggestBox, FontTypography.Body);
        this.FilterAutoSuggestBox.FocusVisualStyle = null;
    }

    public void BindToViewModel(string availableValuesPropertyName, string selectedValuePropertyName) {
        // Bind to AvailableFilterValues for the dropdown suggestions
        _ = this.FilterAutoSuggestBox.SetBinding(
            AutoSuggestBox.OriginalItemsSourceProperty,
            new Binding(availableValuesPropertyName) { Source = this._viewModel, Mode = BindingMode.OneWay }
        );

        // Bind FilterPill Text to SelectedFilterValue (only shows chosen filter, not typed text)
        _ = this.FilterPill.SetBinding(
            Pill.TextProperty,
            new Binding(selectedValuePropertyName) { Source = this._viewModel, Mode = BindingMode.OneWay }
        );

        // Bind FilterPill Visibility to SelectedFilterValue (show only when a filter is selected)
        _ = this.FilterPill.SetBinding(
            VisibilityProperty,
            new Binding(selectedValuePropertyName) {
                Source = this._viewModel,
                Mode = BindingMode.OneWay,
                Converter = VisibilityConverter.Instance
            }
        );
    }

    private void UpdateSelectedFilterValue(string? value) {
        var selectedValueProperty = typeof(TViewModel).GetProperty("SelectedFilterValue");
        if (selectedValueProperty == null) return;

        var currentValue = selectedValueProperty.GetValue(this._viewModel) as string;
        if (currentValue == value) return;

        selectedValueProperty.SetValue(this._viewModel, value);
    }
}