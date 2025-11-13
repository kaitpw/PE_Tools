using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using WpfUiRichTextBox = Wpf.Ui.Controls.RichTextBox;

namespace AddinCmdPalette.Core;

public class SelectableTextBox : UserControl {
    public static readonly DependencyProperty TooltipTextProperty = DependencyProperty.Register(
        nameof(TooltipText),
        typeof(string),
        typeof(SelectableTextBox),
        new PropertyMetadata(string.Empty, OnTooltipTextChanged));

    public static readonly DependencyProperty ReturnFocusTargetProperty = DependencyProperty.Register(
        nameof(ReturnFocusTarget),
        typeof(UIElement),
        typeof(SelectableTextBox),
        new PropertyMetadata(null));

    private WpfUiRichTextBox _richTextBox;
    private ScrollViewer _scrollViewer;
    private Border _border;
    private bool _isInitialized;

    public SelectableTextBox() => this.InitializeControls();

    private void InitializeControls() {
        if (this._isInitialized) return;
        // Create Border - transparent, no border
        this._border = new Border {
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };

        // Create ScrollViewer
        this._scrollViewer = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0)
        };

        // Create RichTextBox
        this._richTextBox = new WpfUiRichTextBox {
            IsReadOnly = true,
            IsTextSelectionEnabled = true,
            Focusable = true,
            FontFamily = new System.Windows.Media.FontFamily("Open Sans"),
            FontSize = (double)(this.TryFindResource("PaletteFontSizeMedium") ?? 6.0),
            Foreground = (System.Windows.Media.Brush)(this.TryFindResource("TextFillColorPrimaryBrush") ?? System.Windows.Media.Brushes.White),
            CaretBrush = (System.Windows.Media.Brush)(this.TryFindResource("TextFillColorPrimaryBrush") ?? System.Windows.Media.Brushes.White),

        };

        this._richTextBox.KeyDown += this.TooltipRichTextBox_KeyDown;
        this._richTextBox.PreviewKeyDown += this.TooltipRichTextBox_PreviewKeyDown;
        this._richTextBox.Loaded += this.TooltipRichTextBox_Loaded;

        // Assemble hierarchy
        this._scrollViewer.Content = this._richTextBox;
        this._border.Child = this._scrollViewer;
        this.Content = this._border;

        this._isInitialized = true;
    }

    public string TooltipText {
        get => (string)this.GetValue(TooltipTextProperty);
        set => this.SetValue(TooltipTextProperty, value);
    }

    public UIElement? ReturnFocusTarget {
        get => (UIElement?)this.GetValue(ReturnFocusTargetProperty);
        set => this.SetValue(ReturnFocusTargetProperty, value);
    }

    public void FocusTooltip() {
        if (this._richTextBox == null) return;

        Debug.WriteLine("[Tooltip] Focus");
        this._richTextBox.Focusable = true;
        _ = this._richTextBox.Focus();
        this._richTextBox.CaretPosition = this._richTextBox.Document.ContentEnd;
    }

    private static void OnTooltipTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is SelectableTextBox panel) {
            panel.UpdateTooltipText();
        }
    }

    private void UpdateTooltipText() {
        if (this._richTextBox.Document == null) return;

        var text = this.TooltipText ?? string.Empty;
        var paragraph = new Paragraph(new Run(text)) {
            LineHeight = 8,
            Margin = new Thickness(0),
            Padding = new Thickness(0),
            TextIndent = 0
        };
        this._richTextBox.Document.Blocks.Clear();
        this._richTextBox.Document.Blocks.Add(paragraph);

        // Set document padding to match the resource
        var docPadding = (Thickness)(this.TryFindResource("PalettePaddingMedium") ?? new Thickness(10, 5, 10, 5));
        this._richTextBox.Document.PagePadding = docPadding;
    }

    private void TooltipRichTextBox_Loaded(object sender, RoutedEventArgs e) => this.UpdateTooltipText();

    private void TooltipRichTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
        // Handle Escape - return focus to search box
        if (e.Key == Key.Escape) {
            Debug.WriteLine("[Tooltip] Esc → Return focus");
            e.Handled = true;
            _ = this.ReturnFocusTarget?.Focus();
        }
    }

    private void TooltipRichTextBox_KeyDown(object sender, KeyEventArgs e) {
        // Escape already handled in PreviewKeyDown
    }
}
