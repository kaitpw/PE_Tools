using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfUiRichTextBox = Wpf.Ui.Controls.RichTextBox;

namespace AddinPaletteSuite.Core.Ui;

public class SelectableTextBox : UserControl, IPopoverExit  {
    public static readonly DependencyProperty TooltipTextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(SelectableTextBox),
        new PropertyMetadata(string.Empty, OnTooltipTextChanged));

    public static readonly DependencyProperty ReturnFocusTargetProperty = DependencyProperty.Register(
        nameof(ReturnFocusTarget),
        typeof(UIElement),
        typeof(SelectableTextBox),
        new PropertyMetadata(null));

    private bool _isInitialized;
    private WpfUiRichTextBox _richTextBox;

    public SelectableTextBox() {
        this.Focusable = true;
        this.InitializeControls();
    }

    public string Text {
        get => (string)this.GetValue(TooltipTextProperty);
        set => this.SetValue(TooltipTextProperty, value);
    }

    public UIElement? ReturnFocusTarget {
        get => (UIElement?)this.GetValue(ReturnFocusTargetProperty);
        set => this.SetValue(ReturnFocusTargetProperty, value);
    }

    public event EventHandler? ExitRequested;

    public void RequestExit() {
        this.ExitRequested?.Invoke(this, EventArgs.Empty);
        _ = this.ReturnFocusTarget?.Focus();
    }

    private void InitializeControls() {
        if (this._isInitialized) return;

        // Create Border using theme resources for consistent popover styling
        var border = new Border {
            Background =
                (Brush)(this.TryFindResource("BackgroundFillColorPrimaryBrush") ??
                        new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 27))),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 8, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Use Wpf.Ui RichTextBox with theme-based styling
        this._richTextBox = new WpfUiRichTextBox {
            IsReadOnly = true,
            Focusable = true,
            IsTextSelectionEnabled = true,
            AutoWordSelection = false,
            FontFamily = new FontFamily("Segoe UI Variable"),
            FontSize = 12.0,
            Foreground = (Brush)(this.TryFindResource("TextFillColorPrimaryBrush") ?? Brushes.Red),
            Width = 250.0,
            MinHeight = 100.0,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        this._richTextBox.PreviewKeyDown += this.RichTextBox_PreviewKeyDown;
        this._richTextBox.LostFocus += this.RichTextBox_LostFocus;

        border.Child = this._richTextBox;
        this.Content = border;

        this._isInitialized = true;
    }

    public void FocusTooltip() {
        if (this._richTextBox == null) return;

        Debug.WriteLine("[Tooltip] Focus");
        _ = this._richTextBox.Focus();

        // Select all text for easy copying
        if (this._richTextBox.Document != null) {
            this._richTextBox.Selection.Select(
                this._richTextBox.Document.ContentStart,
                this._richTextBox.Document.ContentEnd);
        }
    }

    private static void OnTooltipTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is SelectableTextBox panel) panel.UpdateTooltipText();
    }

    private void UpdateTooltipText() {
        if (this._richTextBox == null) return;

        var text = this.Text ?? string.Empty;

        // Create a simple FlowDocument with the text
        var flowDoc = new FlowDocument {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left,
            FontFamily = new FontFamily("Segoe UI Variable"),
            FontSize = 12.0,
            Foreground = (Brush)(this.TryFindResource("TextFillColorPrimaryBrush") ?? Brushes.White)
        };

        var paragraph = new Paragraph(new Run(text)) {
            Margin = new Thickness(0),
            LineHeight = double.NaN, // Auto line height
            FontFamily = new FontFamily("Segoe UI Variable"),
            FontSize = 12.0,
            Foreground = (Brush)(this.TryFindResource("TextFillColorPrimaryBrush") ?? Brushes.White)
        };

        flowDoc.Blocks.Add(paragraph);
        this._richTextBox.Document = flowDoc;

        Debug.WriteLine($"[Tooltip] Updated text: '{text}' (length: {text.Length})");
    }

    private void RichTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
        switch (e.Key) {
        case Key.Escape:
            e.Handled = true;
            this.RequestExit();
            break;
        case Key.Right:
            e.Handled = true;
            this.RequestExit();

            break;
        }
    }

    private void RichTextBox_LostFocus(object sender, RoutedEventArgs e) {
        // Close popover when focus is lost
        var newFocus = Keyboard.FocusedElement as DependencyObject;
        if (newFocus != null && !this.IsAncestorOf(newFocus)) this.RequestExit();
    }
}