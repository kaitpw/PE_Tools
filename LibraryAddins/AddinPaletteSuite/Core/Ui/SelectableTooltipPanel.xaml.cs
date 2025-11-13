using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfUiRichTextBox = Wpf.Ui.Controls.RichTextBox;

namespace AddinPaletteSuite.Core.Ui;

public class SelectableTooltipPanel : UserControl
{
    public static readonly DependencyProperty TooltipTextProperty = DependencyProperty.Register(
        nameof(TooltipText),
        typeof(string),
        typeof(SelectableTooltipPanel),
        new PropertyMetadata(string.Empty, OnTooltipTextChanged));

    public static readonly DependencyProperty ReturnFocusTargetProperty = DependencyProperty.Register(
        nameof(ReturnFocusTarget),
        typeof(UIElement),
        typeof(SelectableTooltipPanel),
        new PropertyMetadata(null));

    private Border _border;
    private bool _isInitialized;

    private WpfUiRichTextBox _richTextBox;
    private ScrollViewer _scrollViewer;

    public SelectableTooltipPanel() => this.InitializeControls();

    public string TooltipText
    {
        get => (string)this.GetValue(TooltipTextProperty);
        set => this.SetValue(TooltipTextProperty, value);
    }

    public UIElement? ReturnFocusTarget
    {
        get => (UIElement?)this.GetValue(ReturnFocusTargetProperty);
        set => this.SetValue(ReturnFocusTargetProperty, value);
    }

    private void InitializeControls()
    {
        if (this._isInitialized) return;
        // Create Border
        this._border = new Border
        {
            Background = (Brush)this.TryFindResource("BackgroundFillColorTertiaryBrush") ?? Brushes.Gray,
            BorderBrush = (Brush)this.TryFindResource("ControlStrokeColorDefaultBrush") ?? Brushes.DarkGray,
            BorderThickness = new Thickness(0, 0, 1, 0)
        };

        // Create ScrollViewer
        this._scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0)
        };

        // Create RichTextBox
        this._richTextBox = new WpfUiRichTextBox
        {
            IsReadOnly = true,
            IsTextSelectionEnabled = true,
            Focusable = true,
            // FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = (double)(this.TryFindResource("PaletteFontSizeMedium") ?? 12.0),
            Background = Brushes.Transparent,
            Foreground = (Brush)(this.TryFindResource("TextFillColorPrimaryBrush") ?? Brushes.White),
            BorderThickness = new Thickness(0),
            CaretBrush = (Brush)(this.TryFindResource("TextFillColorPrimaryBrush") ?? Brushes.White),
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = (Thickness)(this.TryFindResource("PalettePaddingMedium") ?? new Thickness(10, 5, 10, 5)),
            Margin = new Thickness(0)
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

    public void FocusTooltip()
    {
        if (this._richTextBox == null) return;

        Debug.WriteLine("[Tooltip] Focus");
        this._richTextBox.Focusable = true;
        _ = this._richTextBox.Focus();
        this._richTextBox.CaretPosition = this._richTextBox.Document.ContentEnd;
    }

    private static void OnTooltipTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SelectableTooltipPanel panel) panel.UpdateTooltipText();
    }

    private void UpdateTooltipText()
    {
        if (this._richTextBox.Document == null) return;

        var text = this.TooltipText ?? string.Empty;
        var paragraph = new Paragraph(new Run(text)) { LineHeight = 16, Margin = new Thickness(0) };
        this._richTextBox.Document.Blocks.Clear();
        this._richTextBox.Document.Blocks.Add(paragraph);
    }

    private void TooltipRichTextBox_Loaded(object sender, RoutedEventArgs e) => this.UpdateTooltipText();

    private void TooltipRichTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handle Escape - return focus to search box
        if (e.Key == Key.Escape)
        {
            Debug.WriteLine("[Tooltip] Esc → Return focus");
            e.Handled = true;
            _ = this.ReturnFocusTarget?.Focus();
        }
    }

    private void TooltipRichTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        // Escape already handled in PreviewKeyDown
    }
}