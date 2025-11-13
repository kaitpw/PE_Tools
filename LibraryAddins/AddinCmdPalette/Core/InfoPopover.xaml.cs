using AddinCmdPalette.Actions;
using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Binding = System.Windows.Data.Binding;
using Grid = System.Windows.Controls.Grid;
using Visibility = System.Windows.Visibility;
using WpfUiCardControl = Wpf.Ui.Controls.CardControl;


namespace AddinCmdPalette.Core;

/// <summary>
///     Popover control for displaying tooltips or available actions
/// </summary>
public class InfoPopover : UserControl {
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(
        nameof(Action),
        typeof(PaletteAction),
        typeof(InfoPopover),
        new PropertyMetadata(null, OnActionChanged)
    );

    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions),
        typeof(IEnumerable),
        typeof(InfoPopover),
        new PropertyMetadata(null, OnActionsChanged)
    );

    public static readonly DependencyProperty TooltipTextProperty = DependencyProperty.Register(
        nameof(TooltipText),
        typeof(string),
        typeof(InfoPopover),
        new PropertyMetadata(null, OnTooltipTextChanged)
    );

    private ItemsControl _actionsItemsControl;
    private bool _isInitialized;

    private TextBlock _tooltipTextBlock;

    public InfoPopover() {
        this.MinWidth = 300;
        this.MaxWidth = 500;
        this.InitializeControls();
    }

    public PaletteAction? Action {
        get => (PaletteAction?)this.GetValue(ActionProperty);
        set => this.SetValue(ActionProperty, value);
    }

    public IEnumerable? Actions {
        get => (IEnumerable?)this.GetValue(ActionsProperty);
        set => this.SetValue(ActionsProperty, value);
    }

    public string? TooltipText {
        get => (string?)this.GetValue(TooltipTextProperty);
        set => this.SetValue(TooltipTextProperty, value);
    }

    private void InitializeControls() {
        if (this._isInitialized) return;

        // Create CardControl (WPF.UI)
        var cardControl = new WpfUiCardControl {
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Background = (Brush)this.TryFindResource("BackgroundFillColorPrimaryBrush") ?? Brushes.Black
        };

        var grid = new Grid();

        // Create Tooltip TextBlock
        this._tooltipTextBlock = new TextBlock {
            Name = "TooltipTextBlock",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            LineHeight = 18,
            Padding = new Thickness(12, 8, 12, 8),
            Foreground = (Brush)this.TryFindResource("TextFillColorPrimaryBrush") ?? Brushes.White,
            Visibility = Visibility.Collapsed
        };
        _ = grid.Children.Add(this._tooltipTextBlock);

        // Create Actions ItemsControl
        this._actionsItemsControl = new ItemsControl {
            Name = "ActionsItemsControl",
            Visibility = Visibility.Collapsed
        };

        // Create ItemTemplate for actions
        var actionItemTemplate = new DataTemplate();
        var actionBorder = new FrameworkElementFactory(typeof(Border));
        actionBorder.SetValue(BackgroundProperty, Brushes.Transparent);
        actionBorder.SetValue(PaddingProperty, new Thickness(12, 8, 12, 8));
        actionBorder.SetValue(CursorProperty, Cursors.Hand);
        actionBorder.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(this.ActionItem_MouseLeftButtonUp));

        // Border Style with hover trigger
        var borderStyle = new Style(typeof(Border));
        borderStyle.Setters.Add(new Setter(BackgroundProperty, Brushes.Transparent));
        var hoverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(BackgroundProperty,
            (Brush)this.TryFindResource("BackgroundFillColorSecondaryBrush") ?? Brushes.DarkGray));
        borderStyle.Triggers.Add(hoverTrigger);
        actionBorder.SetValue(StyleProperty, borderStyle);

        var actionGrid = new FrameworkElementFactory(typeof(Grid));

        var actionColumn1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        actionColumn1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var actionColumn2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        actionColumn2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);

        actionGrid.AppendChild(actionColumn1);
        actionGrid.AppendChild(actionColumn2);

        var actionNameTextBlock = new FrameworkElementFactory(typeof(TextBlock));
        actionNameTextBlock.SetBinding(TextBlock.TextProperty, new Binding("Name"));
        actionNameTextBlock.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI Variable"));
        actionNameTextBlock.SetValue(TextBlock.FontSizeProperty, 12.0);
        actionNameTextBlock.SetValue(TextBlock.ForegroundProperty,
            (Brush)this.TryFindResource("TextFillColorPrimaryBrush") ?? Brushes.White);
        actionNameTextBlock.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        actionNameTextBlock.SetValue(Grid.ColumnProperty, 0);
        actionGrid.AppendChild(actionNameTextBlock);

        var shortcutTextBlock = new FrameworkElementFactory(typeof(TextBlock));
        shortcutTextBlock.SetValue(NameProperty, "ShortcutTextBlock");
        shortcutTextBlock.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI Variable"));
        shortcutTextBlock.SetValue(TextBlock.FontSizeProperty, 11.0);
        shortcutTextBlock.SetValue(TextBlock.ForegroundProperty,
            (Brush)this.TryFindResource("TextFillColorTertiaryBrush") ?? Brushes.Gray);
        shortcutTextBlock.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        shortcutTextBlock.SetValue(MarginProperty, new Thickness(12, 0, 0, 0));
        shortcutTextBlock.SetValue(Grid.ColumnProperty, 1);
        shortcutTextBlock.AddHandler(LoadedEvent, new RoutedEventHandler(this.ShortcutTextBlock_Loaded));
        actionGrid.AppendChild(shortcutTextBlock);

        actionBorder.AppendChild(actionGrid);
        actionItemTemplate.VisualTree = actionBorder;
        this._actionsItemsControl.ItemTemplate = actionItemTemplate;

        _ = grid.Children.Add(this._actionsItemsControl);

        cardControl.Content = grid;
        this.Content = cardControl;

        this._isInitialized = true;
    }

    public event EventHandler<PaletteAction>? ActionClicked;

    private static void OnActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is InfoPopover popover) popover.UpdateDisplay();
    }

    private static void OnActionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is InfoPopover popover) {
            popover._actionsItemsControl.ItemsSource = e.NewValue as IEnumerable;
            popover.UpdateDisplay();
        }
    }

    private static void OnTooltipTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is InfoPopover popover) {
            popover._tooltipTextBlock.Text = e.NewValue as string ?? string.Empty;
            popover.UpdateDisplay();
        }
    }

    private void UpdateDisplay() {
        if (!string.IsNullOrEmpty(this.TooltipText)) {
            this._tooltipTextBlock.Visibility = Visibility.Visible;
            this._actionsItemsControl.Visibility = Visibility.Collapsed;
        } else if (this.Actions != null) {
            this._tooltipTextBlock.Visibility = Visibility.Collapsed;
            this._actionsItemsControl.Visibility = Visibility.Visible;
        } else {
            this._tooltipTextBlock.Visibility = Visibility.Collapsed;
            this._actionsItemsControl.Visibility = Visibility.Collapsed;
        }
    }

    private void ActionItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (sender is FrameworkElement element && element.DataContext is PaletteAction action) {
            Debug.WriteLine($"[Popover] Click: {action.Name}");
            this.ActionClicked?.Invoke(this, action);
            e.Handled = true;
        }
    }

    private void ShortcutTextBlock_Loaded(object sender, RoutedEventArgs e) {
        if (sender is TextBlock textBlock) {
            var parent = textBlock.Parent as FrameworkElement;
            while (parent != null && parent.DataContext is not PaletteAction)
                parent = parent.Parent as FrameworkElement;

            if (parent?.DataContext is PaletteAction action) {
                var shortcut = this.FormatShortcut(action);
                textBlock.Text = shortcut;
                textBlock.Visibility = string.IsNullOrEmpty(shortcut) ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }

    private string FormatShortcut(PaletteAction action) {
        var parts = new List<string>();

        if (action.Modifiers != ModifierKeys.None) {
            if ((action.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((action.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((action.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        }

        if (action.Key.HasValue) {
            var keyStr = action.Key.Value.ToString();
            if (keyStr == "Return") keyStr = "Enter";
            parts.Add(keyStr);
        }

        if (action.MouseButton.HasValue) {
            var buttonStr = action.MouseButton.Value switch {
                MouseButton.Left => "Click",
                MouseButton.Right => "Right-Click",
                MouseButton.Middle => "Middle-Click",
                MouseButton.XButton1 => "X1-Click",
                MouseButton.XButton2 => "X2-Click",
                _ => action.MouseButton.Value.ToString()
            };
            parts.Add(buttonStr);
        }

        return parts.Count > 0 ? string.Join("+", parts) : string.Empty;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (e.Property == TooltipTextProperty || e.Property == ActionsProperty) this.UpdateDisplay();
    }
}