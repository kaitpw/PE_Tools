#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Wpf.Ui.Markup;
using WpfUiRichTextBox = Wpf.Ui.Controls.RichTextBox;
using Visibility = System.Windows.Visibility;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Selectable text display component with keyboard navigation
/// </summary>
public class SelectableTextBox : UserControl, IPopoverExit {
    private readonly WpfUiRichTextBox _richTextBox;

    public SelectableTextBox() {
        this.Focusable = true;

        this._richTextBox = new WpfUiRichTextBox {
            IsReadOnly = true,
            Focusable = true,
            IsTextSelectionEnabled = true,
            AutoWordSelection = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        this._richTextBox.PreviewKeyDown += this.RichTextBox_PreviewKeyDown;
        this._richTextBox.LostFocus += this.RichTextBox_LostFocus;

        this.Content = new BorderSpec()
            .Background(ThemeResource.ApplicationBackgroundBrush)
            .HorizontalAlign(HorizontalAlignment.Left)
            .VerticalAlign(VerticalAlignment.Top)
            .Width(150)
            .Height(50, 75, 400)
            .Border(thickness: UiSz.ss)
            .Padding(UiSz.m)
            .CreateAround(this._richTextBox);
    }

    public UIElement? ReturnFocusTarget { get; set; }

    public event EventHandler? ExitRequested;

    public void RequestExit() {
        this.ExitRequested?.Invoke(this, EventArgs.Empty);
        _ = this.ReturnFocusTarget?.Focus();
    }

    /// <summary>
    ///     Shows the text box with the specified text and focuses it
    /// </summary>
    public void Show(string? text = null) {
        this.UpdateContent(text);
        this.Visibility = Visibility.Visible;
    }

    /// <summary>
    ///     Hides the text box
    /// </summary>
    public void Hide() => this.Visibility = Visibility.Collapsed;

    private void UpdateContent(string? text) {
        this._richTextBox.Document = new FlowDocument {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left,
            FontFamily = ThemeManager.FontFamily(),
            FontSize = 11,
            LineHeight = 15.0 // Matching Body style line height
        };
        // Set foreground from DynamicResource
        this._richTextBox.Document.SetResourceReference(FlowDocument.ForegroundProperty, "TextFillColorSecondaryBrush");

        this._richTextBox.Document.Blocks.Add(new Paragraph(new Run(text)));
    }

    private void RichTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
        e.Handled = true;
        this.RequestExit();
    }

    private void RichTextBox_LostFocus(object sender, RoutedEventArgs e) {
        var newFocus = Keyboard.FocusedElement as DependencyObject;
        if (newFocus != null && !this.IsAncestorOf(newFocus)) this.RequestExit();
    }
}