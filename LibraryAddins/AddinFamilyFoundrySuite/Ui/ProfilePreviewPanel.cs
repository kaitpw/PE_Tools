using AddinFamilyFoundrySuite.Core;
using PeUi.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Wpf.Ui.Markup;
using WpfUiRichTextBox = Wpf.Ui.Controls.RichTextBox;

namespace AddinFamilyFoundrySuite.Ui;

/// <summary>
///     Side panel that displays profile preview data including operations, parameters, and families.
///     Designed to be used as a sidebar in the palette.
/// </summary>
public class ProfilePreviewPanel : UserControl {
    private readonly WpfUiRichTextBox _richTextBox;

    public ProfilePreviewPanel() {
        // Create scrollable rich text box for content display
        this._richTextBox = new WpfUiRichTextBox {
            IsReadOnly = true,
            Focusable = false,
            IsTextSelectionEnabled = true,
            AutoWordSelection = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var border = new BorderSpec()
            .Background(ThemeResource.ApplicationBackgroundBrush)
            .Padding(UiSz.m)
            .CreateAround(this._richTextBox);

        this.Content = border;
    }

    /// <summary>
    ///     Updates the preview panel with new data.
    /// </summary>
    public void UpdatePreview(PreviewData data) => this.UpdateContent(data);

    private void UpdateContent(PreviewData data) {
        if (data == null) {
            this._richTextBox.Document = new FlowDocument();
            return;
        }

        var doc = new FlowDocument {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left,
            FontFamily = ThemeManager.FontFamily(),
            FontSize = 11,
            LineHeight = 15.0
        };
        doc.SetResourceReference(FlowDocument.ForegroundProperty, "TextFillColorSecondaryBrush");

        // Profile name header
        var headerPara = new Paragraph(new Run(data.ProfileName) { FontWeight = FontWeights.Bold, FontSize = 14 }) {
            Margin = new Thickness(0, 0, 0, 8)
        };
        doc.Blocks.Add(headerPara);

        // Validation Status Section (if there are fixes or errors)
        if (!data.IsValid || data.AppliedFixes.Any() || data.RemainingErrors.Any()) {
            AddValidationSection(doc, data);
        }

        // Only show operations/params/families if profile is valid
        if (data.IsValid) {
            // Summary section
            var summaryPara = new Paragraph();
            summaryPara.Inlines.Add(new Run($"Operations: {data.OperationCount}") { FontWeight = FontWeights.SemiBold });
            summaryPara.Inlines.Add(new LineBreak());
            summaryPara.Inlines.Add(new Run($"APS Parameters: {data.ApsParameterCount}"));
            summaryPara.Inlines.Add(new LineBreak());
            summaryPara.Inlines.Add(new Run($"Families: {data.FamilyCount}"));
            summaryPara.Margin = new Thickness(0, 0, 0, 12);
            doc.Blocks.Add(summaryPara);

            // Operations list
            if (data.Operations.Count > 0) {
                var opHeader = new Paragraph(new Run("Operations") { FontWeight = FontWeights.SemiBold }) {
                    Margin = new Thickness(0, 0, 0, 4)
                };
                doc.Blocks.Add(opHeader);

                var opList = new List { MarkerStyle = TextMarkerStyle.Decimal, Margin = new Thickness(16, 0, 0, 12) };
                foreach (var op in data.Operations) {
                    var listItem = new ListItem(new Paragraph(new Run($"{op.Name} ({op.Type})")));
                    opList.ListItems.Add(listItem);
                }
                doc.Blocks.Add(opList);
            }

            // APS Parameters list (abbreviated)
            if (data.ApsParameterNames.Count > 0) {
                var paramHeader = new Paragraph(new Run("APS Parameters") { FontWeight = FontWeights.SemiBold }) {
                    Margin = new Thickness(0, 0, 0, 4)
                };
                doc.Blocks.Add(paramHeader);

                var maxShow = Math.Min(data.ApsParameterNames.Count, 10);
                var paramList = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(16, 0, 0, 12) };
                for (var i = 0; i < maxShow; i++) {
                    var listItem = new ListItem(new Paragraph(new Run(data.ApsParameterNames[i])));
                    paramList.ListItems.Add(listItem);
                }
                if (data.ApsParameterNames.Count > maxShow) {
                    var more = new ListItem(new Paragraph(new Run($"... and {data.ApsParameterNames.Count - maxShow} more")));
                    paramList.ListItems.Add(more);
                }
                doc.Blocks.Add(paramList);
            }

            // Families list (abbreviated)
            if (data.FamilyNames.Count > 0) {
                var famHeader = new Paragraph(new Run("Families to Process") { FontWeight = FontWeights.SemiBold }) {
                    Margin = new Thickness(0, 0, 0, 4)
                };
                doc.Blocks.Add(famHeader);

                var maxShow = Math.Min(data.FamilyNames.Count, 10);
                var famList = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(16, 0, 0, 12) };
                for (var i = 0; i < maxShow; i++) {
                    var listItem = new ListItem(new Paragraph(new Run(data.FamilyNames[i])));
                    famList.ListItems.Add(listItem);
                }
                if (data.FamilyNames.Count > maxShow) {
                    var more = new ListItem(new Paragraph(new Run($"... and {data.FamilyNames.Count - maxShow} more")));
                    famList.ListItems.Add(more);
                }
                doc.Blocks.Add(famList);
            }
        }

        this._richTextBox.Document = doc;
    }

    private static void AddValidationSection(FlowDocument doc, PreviewData data) {
        // Status indicator
        var statusPara = new Paragraph {
            Margin = new Thickness(0, 0, 0, 8)
        };

        if (data.IsValid) {
            var validRun = new Run("✓ Valid Profile") {
                FontWeight = FontWeights.Bold,
                FontSize = 12
            };
            validRun.SetResourceReference(Run.ForegroundProperty, "SystemFillColorSuccessBrush");
            statusPara.Inlines.Add(validRun);
        } else {
            var invalidRun = new Run("✗ Invalid Profile") {
                FontWeight = FontWeights.Bold,
                FontSize = 12
            };
            invalidRun.SetResourceReference(Run.ForegroundProperty, "SystemFillColorCriticalBrush");
            statusPara.Inlines.Add(invalidRun);
        }
        doc.Blocks.Add(statusPara);

        // Applied fixes section (green)
        if (data.AppliedFixes.Any()) {
            var fixesHeader = new Paragraph(new Run("Applied Fixes") { FontWeight = FontWeights.SemiBold }) {
                Margin = new Thickness(0, 8, 0, 4)
            };
            fixesHeader.SetResourceReference(Paragraph.ForegroundProperty, "SystemFillColorSuccessBrush");
            doc.Blocks.Add(fixesHeader);

            var fixesList = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(16, 0, 0, 12) };
            foreach (var fix in data.AppliedFixes) {
                var para = new Paragraph(new Run(fix));
                para.SetResourceReference(Paragraph.ForegroundProperty, "SystemFillColorSuccessBrush");
                var listItem = new ListItem(para);
                fixesList.ListItems.Add(listItem);
            }
            doc.Blocks.Add(fixesList);
        }

        // Remaining errors section (red)
        if (data.RemainingErrors.Any()) {
            var errorsHeader = new Paragraph(new Run("Validation Errors") { FontWeight = FontWeights.SemiBold }) {
                Margin = new Thickness(0, 8, 0, 4)
            };
            errorsHeader.SetResourceReference(Paragraph.ForegroundProperty, "SystemFillColorCriticalBrush");
            doc.Blocks.Add(errorsHeader);

            var errorsList = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(16, 0, 0, 12) };
            foreach (var error in data.RemainingErrors) {
                var para = new Paragraph(new Run(error));
                para.SetResourceReference(Paragraph.ForegroundProperty, "SystemFillColorCriticalBrush");
                var listItem = new ListItem(para);
                errorsList.ListItems.Add(listItem);
            }
            doc.Blocks.Add(errorsList);
        }
    }
}

/// <summary>
///     Data model for profile preview display.
/// </summary>
public class PreviewData {
    public string ProfileName { get; init; } = string.Empty;
    public int OperationCount => this.Operations.Count;
    public int ApsParameterCount => this.ApsParameterNames.Count;
    public int FamilyCount => this.FamilyNames.Count;
    public List<OperationInfo> Operations { get; init; } = [];
    public List<string> ApsParameterNames { get; init; } = [];
    public List<string> FamilyNames { get; init; } = [];

    // File metadata (from ProfileListItem)
    public string FilePath { get; init; } = string.Empty;
    public DateTime? CreatedDate { get; init; }
    public DateTime? ModifiedDate { get; init; }
    public int LineCount { get; init; }

    // Validation status
    public bool IsValid { get; init; } = true;
    public List<string> AppliedFixes { get; init; } = [];
    public List<string> RemainingErrors { get; init; } = [];
}

/// <summary>
///     Operation info for preview display.
/// </summary>
public record OperationInfo(string Name, string Description, string Type, string IsMerged);

