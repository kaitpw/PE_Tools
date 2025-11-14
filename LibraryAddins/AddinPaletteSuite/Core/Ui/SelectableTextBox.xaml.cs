#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfUiRichTextBox = Wpf.Ui.Controls.RichTextBox;
using Theme = AddinPaletteSuite.Core.Ui.ThemeManager;
using Visibility = System.Windows.Visibility;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Selectable text display component with keyboard navigation
/// </summary>
public class SelectableTextBox : UserControl, IPopoverExit {
    private readonly WpfUiRichTextBox _richTextBox;

    public SelectableTextBox() {
        this.Focusable = true;
        this.FocusVisualStyle = null;

        this._richTextBox = new WpfUiRichTextBox {
            IsReadOnly = true,
            Focusable = true,
            IsTextSelectionEnabled = true,
            AutoWordSelection = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FocusVisualStyle = null
        };

        this._richTextBox.PreviewKeyDown += this.RichTextBox_PreviewKeyDown;
        this._richTextBox.LostFocus += this.RichTextBox_LostFocus;

        var border = new Border {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = Brushes.Transparent,
            Child = this._richTextBox,
            Width = 200.0,
            MinHeight = 100.0
        };

        this.Content = border
            .ApplyBorder(bgColor: Theme.PrimaryAccent)
            .WithPadding(UiSz.m, UiSz.m);
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

        // Focus and select all text after render
        _ = this.Dispatcher.BeginInvoke(new Action(() => {
            _ = this._richTextBox.Focus();
            this._richTextBox.Selection.Select(
                this._richTextBox.Document.ContentStart,
                this._richTextBox.Document.ContentEnd);
        }), DispatcherPriority.Loaded);
    }

    /// <summary>
    ///     Hides the text box
    /// </summary>
    public void Hide() => this.Visibility = Visibility.Collapsed;

    private void UpdateContent(string? text) {
        this._richTextBox.Document = new FlowDocument {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left,
            FontFamily = Theme.FontFamily,
            FontSize = (double)TxtSz.normal,
            Foreground = Theme.SecondaryTextAccent
        };
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