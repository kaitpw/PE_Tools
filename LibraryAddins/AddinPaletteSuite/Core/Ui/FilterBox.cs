#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using Binding = System.Windows.Data.Binding;

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

    protected void IconBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => this.ToggleExpanded();

    protected void FilterAutoSuggestBox_GotFocus(object sender, RoutedEventArgs e) {
        if (!this._isExpanded) this.Expand();
    }

    protected void FilterAutoSuggestBox_LostFocus(object sender, RoutedEventArgs e) {
        // Only collapse if the text is empty
        if (string.IsNullOrEmpty(this.FilterAutoSuggestBox.Text)) this.Collapse();
    }

    protected void ToggleExpanded() {
        if (this._isExpanded)
            this.Collapse();
        else
            this.Expand();
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

        // Apply styling after InitializeComponent is called
        this.ApplyStyles();

        // Wire up events 
        this.FilterAutoSuggestBox.QuerySubmitted += this.FilterBox_QuerySubmitted;
        this.FilterAutoSuggestBox.PreviewKeyDown += this.FilterAutoSuggestBox_PreviewKeyDown;
        this.FilterAutoSuggestBox.KeyDown += this.FilterAutoSuggestBox_KeyDown;
    }

    private void FilterAutoSuggestBox_PreviewKeyDown(object sender, KeyEventArgs e) {
        switch (e.Key) {
        case Key.Tab:
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift) {
                e.Handled = true;
                this.RequestExit();
            }

            break;

        case Key.Escape:
            e.Handled = true;
            this.RequestExit();
            break;

        case Key.Enter:
            _ = this.Dispatcher.BeginInvoke(
                new Action(this.RequestExit),
                DispatcherPriority.Background
            );
            break;
        }
    }

    private void FilterAutoSuggestBox_KeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.Enter) {
            // Mark as handled at the palette level to prevent item execution
            // By using KeyDown (not PreviewKeyDown), AutoSuggestBox has already processed it
            e.Handled = true;
        }
    }

    private void ApplyStyles() {
        // Apply corner radius
        var topRadius = ThemeManager.Radius.TopLeft;
        this.FilterBorder.CornerRadius = new CornerRadius(topRadius, topRadius, 0, 0);
        _ = this.FilterBorder
            .WithSpacing(0, 0)
            .WithPadding(UiSz.ll, UiSz.ll, UiSz.ll, UiSz.ll);

        // Apply typography style
        ThemeManager.ApplyTypographyStyle(this.FilterAutoSuggestBox, FontTypography.Body);
        this.FilterAutoSuggestBox.FocusVisualStyle = null;
    }

    public void BindToViewModel(string availableValuesPropertyName, string selectedValuePropertyName) {
        // Bind to AvailableFilterValues
        _ = this.FilterAutoSuggestBox.SetBinding(
            AutoSuggestBox.OriginalItemsSourceProperty,
            new Binding(availableValuesPropertyName) { Source = this._viewModel, Mode = BindingMode.OneWay }
        );

        // Bind to SelectedFilterValue
        _ = this.FilterAutoSuggestBox.SetBinding(
            AutoSuggestBox.TextProperty,
            new Binding(selectedValuePropertyName) {
                Source = this._viewModel,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            }
        );
    }

    private void FilterBox_QuerySubmitted(object sender, AutoSuggestBoxQuerySubmittedEventArgs e) {
        var availableValuesProperty = typeof(TViewModel).GetProperty("AvailableFilterValues");
        var selectedValueProperty = typeof(TViewModel).GetProperty("SelectedFilterValue");

        if (availableValuesProperty == null || selectedValueProperty == null) return;

        var availableValues = availableValuesProperty.GetValue(this._viewModel) as IEnumerable<string>;

        if (!string.IsNullOrEmpty(e.QueryText)) {
            // Check if the query matches an available filter value
            var matchingValue = availableValues
                ?.FirstOrDefault(val => val.Equals(e.QueryText, StringComparison.OrdinalIgnoreCase));

            if (matchingValue != null) {
                selectedValueProperty.SetValue(this._viewModel, matchingValue);
            } else {
                // If no exact match, try to find first partial match
                var partialMatch = availableValues
                    ?.FirstOrDefault(val => val.Contains(e.QueryText, StringComparison.OrdinalIgnoreCase));

                if (partialMatch != null) {
                    selectedValueProperty.SetValue(this._viewModel, partialMatch);
                    this.FilterAutoSuggestBox.Text = partialMatch;
                }
            }
        } else {
            selectedValueProperty.SetValue(this._viewModel, string.Empty);
        }

        _ = this.Dispatcher.BeginInvoke(new Action(this.RequestExit), DispatcherPriority.Input);
    }
}