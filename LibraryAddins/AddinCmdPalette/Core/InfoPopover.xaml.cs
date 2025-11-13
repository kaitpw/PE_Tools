using AddinCmdPalette.Actions;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AddinCmdPalette.Core;

/// <summary>
///     Popover control for displaying tooltips or available actions
/// </summary>
public partial class InfoPopover : UserControl {
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(
        nameof(Action),
        typeof(PaletteAction),
        typeof(InfoPopover),
        new PropertyMetadata(null, OnActionChanged)
    );

    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions),
        typeof(System.Collections.IEnumerable),
        typeof(InfoPopover),
        new PropertyMetadata(null, OnActionsChanged)
    );

    public static readonly DependencyProperty TooltipTextProperty = DependencyProperty.Register(
        nameof(TooltipText),
        typeof(string),
        typeof(InfoPopover),
        new PropertyMetadata(null, OnTooltipTextChanged)
    );

    public InfoPopover() {
        this.InitializeComponent();
    }

    public PaletteAction? Action {
        get => (PaletteAction?)this.GetValue(ActionProperty);
        set => this.SetValue(ActionProperty, value);
    }

    public System.Collections.IEnumerable? Actions {
        get => (System.Collections.IEnumerable?)this.GetValue(ActionsProperty);
        set => this.SetValue(ActionsProperty, value);
    }

    public string? TooltipText {
        get => (string?)this.GetValue(TooltipTextProperty);
        set => this.SetValue(TooltipTextProperty, value);
    }

    public event EventHandler<PaletteAction>? ActionClicked;

    private static void OnActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is InfoPopover popover) popover.UpdateDisplay();
    }

    private static void OnActionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is InfoPopover popover) {
            popover.ActionsItemsControl.ItemsSource = e.NewValue as System.Collections.IEnumerable;
            popover.UpdateDisplay();
        }
    }

    private static void OnTooltipTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is InfoPopover popover) {
            popover.TooltipTextBlock.Text = e.NewValue as string ?? string.Empty;
            popover.UpdateDisplay();
        }
    }

    private void UpdateDisplay() {
        if (!string.IsNullOrEmpty(this.TooltipText)) {
            this.TooltipTextBlock.Visibility = System.Windows.Visibility.Visible;
            this.ActionsItemsControl.Visibility = System.Windows.Visibility.Collapsed;
        } else if (this.Actions != null) {
            this.TooltipTextBlock.Visibility = System.Windows.Visibility.Collapsed;
            this.ActionsItemsControl.Visibility = System.Windows.Visibility.Visible;
        } else {
            this.TooltipTextBlock.Visibility = System.Windows.Visibility.Collapsed;
            this.ActionsItemsControl.Visibility = System.Windows.Visibility.Collapsed;
        }
    }

    private void ActionItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (sender is FrameworkElement element && element.DataContext is PaletteAction action) {
            this.ActionClicked?.Invoke(this, action);
            e.Handled = true;
        }
    }

    private void ShortcutTextBlock_Loaded(object sender, RoutedEventArgs e) {
        if (sender is TextBlock textBlock) {
            var parent = textBlock.Parent as FrameworkElement;
            while (parent != null && parent.DataContext is not PaletteAction) {
                parent = parent.Parent as FrameworkElement;
            }

            if (parent?.DataContext is PaletteAction action) {
                var shortcut = this.FormatShortcut(action);
                textBlock.Text = shortcut;
                textBlock.Visibility = string.IsNullOrEmpty(shortcut) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
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

