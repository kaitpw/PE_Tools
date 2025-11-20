#nullable enable

using PeUi.Core;
using PeUi.Core.Converters;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using Binding = System.Windows.Data.Binding;

namespace PeUi.Components;

/// <summary>
///     Non-generic base class for FilterBox.xaml
///     This matches the XAML x:Class declaration and provides access to XAML-defined controls
/// </summary>
public partial class FilterBox : UserControl, IPopoverExit {
    private Storyboard? _collapseStoryboard;
    private Storyboard? _expandStoryboard;
    private bool _isExpanded;

    protected FilterBox() {
        this.InitializeComponent();
        this.Loaded += this.FilterBox_Loaded;
    }

    public UIElement? ReturnFocusTarget { get; set; }
    public event EventHandler? ExitRequested;

    public void RequestExit() {
        this.ExitRequested?.Invoke(this, EventArgs.Empty);
        _ = this.ReturnFocusTarget?.Focus();
    }

    private void FilterBox_Loaded(object sender, RoutedEventArgs e) => this.CreateStoryboards();

    private void CreateStoryboards() {
        // Create ExpandStoryboard
        this._expandStoryboard = new Storyboard();
        var expandWidthAnimation = new DoubleAnimation {
            To = 150.0,
            Duration = new Duration(TimeSpan.FromSeconds(0.2)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(expandWidthAnimation, this.FilterAutoSuggestBox);
        Storyboard.SetTargetProperty(expandWidthAnimation, new PropertyPath("Width"));
        this._expandStoryboard.Children.Add(expandWidthAnimation);

        var expandOpacityAnimation = new DoubleAnimation {
            To = 1.0,
            Duration = new Duration(TimeSpan.FromSeconds(0.15)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(expandOpacityAnimation, this.FilterAutoSuggestBox);
        Storyboard.SetTargetProperty(expandOpacityAnimation, new PropertyPath("Opacity"));
        this._expandStoryboard.Children.Add(expandOpacityAnimation);

        // Create CollapseStoryboard
        this._collapseStoryboard = new Storyboard();
        var collapseWidthAnimation = new DoubleAnimation {
            To = 0.0,
            Duration = new Duration(TimeSpan.FromSeconds(0.15)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(collapseWidthAnimation, this.FilterAutoSuggestBox);
        Storyboard.SetTargetProperty(collapseWidthAnimation, new PropertyPath("Width"));
        this._collapseStoryboard.Children.Add(collapseWidthAnimation);

        var collapseOpacityAnimation = new DoubleAnimation {
            To = 0.0,
            Duration = new Duration(TimeSpan.FromSeconds(0.1)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(collapseOpacityAnimation, this.FilterAutoSuggestBox);
        Storyboard.SetTargetProperty(collapseOpacityAnimation, new PropertyPath("Opacity"));
        this._collapseStoryboard.Children.Add(collapseOpacityAnimation);
    }

    /// <summary>
    ///     Focuses the FilterBox by expanding and focusing the AutoSuggestBox
    /// </summary>
    public new void Focus() => this.Expand();

    protected void IconBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _ = this.FilterAutoSuggestBox.Focus();

    protected void ClearFilterBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        this.OnClearFilterRequested();
        e.Handled = true;
    }

    protected virtual void OnClearFilterRequested() {
        // Override in derived class to clear filter
    }

    protected void FilterAutoSuggestBox_GotFocus(object sender, RoutedEventArgs e) {
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

        this._expandStoryboard?.Begin();

        // Focus the AutoSuggestBox after expansion starts
        _ = this.Dispatcher.BeginInvoke(new Action(() => _ = this.FilterAutoSuggestBox.Focus()),
            DispatcherPriority.Input);
    }

    protected void Collapse() {
        if (!this._isExpanded) return;
        this._isExpanded = false;

        this._collapseStoryboard?.Begin();
    }
}

/// <summary>
///     Generic FilterBox implementation with typed ViewModel support
///     Provides filtering functionality with AutoSuggestBox
/// </summary>
public class FilterBox<TViewModel> : FilterBox where TViewModel : class {
    private readonly TViewModel _viewModel;
    private string? _availableValuesPropertyName;

    public FilterBox(TViewModel viewModel) {
        this._viewModel = viewModel;
        this.FilterAutoSuggestBox.SuggestionChosen += this.FilterAutoSuggestBox_SuggestionChosen;
        this.FilterAutoSuggestBox.PreviewKeyDown += this.FilterAutoSuggestBox_PreviewKeyDown;
    }


    protected override void OnClearFilterRequested() => this.UpdateSelectedFilterValue(null);

    /// <summary>
    ///     This event fires EVERY time a list item is focused by the keyboard. update view model here.
    /// </summary>
    private void FilterAutoSuggestBox_SuggestionChosen(object sender, AutoSuggestBoxSuggestionChosenEventArgs e) {
        this.UpdateSelectedFilterValue(e.SelectedItem.ToString());
        e.Handled = true;
    }

    /// <summary>
    ///     Handle escaping and unfocusing the FilterBox here.
    /// </summary>
    private void FilterAutoSuggestBox_PreviewKeyDown(object sender, KeyEventArgs e) {
        // handle return focus to main search box and hanlde return focus to filter searchbox. 
        if (e.Key is Key.Tab or Key.Escape) {
            e.Handled = true;
            this.UpdateSelectedFilterValue(null);
            this.RequestExit();
        } else if (e.Key is Key.Enter) {
            this.UpdateSelectedFilterValue(this.FilterAutoSuggestBox.Text);
            e.Handled = true;
            this.RequestExit();
        } else if (e.Key is not Key.Up and not Key.Down) _ = this.FilterAutoSuggestBox.Focus();
    }

    public void BindToViewModel(string availableValuesPropertyName, string selectedValuePropertyName) {
        this._availableValuesPropertyName = availableValuesPropertyName;

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

        // Bind ClearFilterBorder Visibility to SelectedFilterValue (show X button when filter is active)
        if (this.FindName("ClearFilterBorder") is Border clearFilterBorder) {
            _ = clearFilterBorder.SetBinding(
                VisibilityProperty,
                new Binding(selectedValuePropertyName) {
                    Source = this._viewModel,
                    Mode = BindingMode.OneWay,
                    Converter = VisibilityConverter.Instance
                }
            );
        }
    }

    private void UpdateSelectedFilterValue(string? value) {
        var selectedValueProperty = typeof(TViewModel).GetProperty("SelectedFilterValue");
        var currentValue = selectedValueProperty?.GetValue(this._viewModel) as string;
        if (currentValue == value) return;

        if (string.IsNullOrEmpty(value)) {
            // enable clearing the value
            selectedValueProperty?.SetValue(this._viewModel, value);
            return;
        }

        // Validate that the value exists in available filter values
        if (string.IsNullOrEmpty(this._availableValuesPropertyName)) return;
        var availableValuesProperty = typeof(TViewModel).GetProperty(this._availableValuesPropertyName);
        if (availableValuesProperty?.GetValue(this._viewModel) is not ObservableCollection<string> availableValues)
            return;

        if (!availableValues.Contains(value)) return;
        selectedValueProperty?.SetValue(this._viewModel, value);
    }
}