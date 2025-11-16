#nullable enable

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace AddinPaletteSuite.Core.Ui;

public enum UiSz {
    ss = 1,
    s = 2,
    m = 4,
    l = 6,
    ll = 9
}

public enum TxtSz {
    ss = 6,
    s = 8,
    normal = 10,
    m = 12,
    l = 14,
    ll = 16
}

/// <summary>
///     Theme settings for dimensions, spacing, tooltips, and item states.
/// </summary>
internal static class ThemeSettings {
    internal static double IconOpacity { get; } = 0.8;
    internal static CornerRadius Radius { get; } = new(6);
    internal static double DisabledOpacity { get; } = 0.4;
}

/// <summary>
///     Centralized theme management for palette UI controls.
///     Wraps ApplicationAccentColorManager and provides type-safe access to colors, typography, and spacing.
/// </summary>
public static class ThemeManager {
    public static double IconOpacity => ThemeSettings.IconOpacity;
    public static CornerRadius Radius => ThemeSettings.Radius;

    public static double DisabledOpacity => ThemeSettings.DisabledOpacity;

    // Font Family
    public static FontFamily FontFamily() => new("Segoe UI Variable Text");

    /// <summary>
    ///     Gets a typography style by FontTypography enum.
    ///     Loads the style from XAML resources defined in TypographyOverrides.xaml.
    ///     The targetType parameter is kept for API compatibility but styles are TextBlock-based.
    /// </summary>
    /// <param name="typography">The FontTypography level to get</param>
    /// <param name="_">Unused type parameter for API compatibility</param>
    /// <param name="searchContext">Optional element to search for resources in its resource chain before Application</param>
    public static Style GetTypographyStyle(FontTypography typography,
        Type? _ = null,
        FrameworkElement? searchContext = null) {
        // Map FontTypography enum to XAML resource key
        var styleKey = typography switch {
            FontTypography.Caption => "CaptionTextBlockStyle",
            FontTypography.Body => "BodyTextBlockStyle",
            FontTypography.BodyStrong => "BodyStrongTextBlockStyle",
            FontTypography.Subtitle => "SubtitleTextBlockStyle",
            FontTypography.Title => "TitleTextBlockStyle",
            FontTypography.TitleLarge => "TitleLargeTextBlockStyle",
            FontTypography.Display => "DisplayTextBlockStyle",
            _ => throw new ArgumentOutOfRangeException(nameof(typography), typography, null)
        };

        // Try to find the style - first in searchContext, then in Application.Current
        Style? style = null;
        if (searchContext != null) style = searchContext.TryFindResource(styleKey) as Style;

        if (style == null) style = Application.Current?.TryFindResource(styleKey) as Style;

        if (style is null) {
            throw new InvalidOperationException(
                $"Typography style '{styleKey}' not found in application resources. " +
                "Ensure TypographyOverrides.xaml is loaded in WpfUiResources.xaml.");
        }

        return style;
    }

    /// <summary>
    ///     Creates a ResourceDictionary with implicit styles for specific controls.
    ///     Note: TextBlock styles are now handled by CreateTypographyOverrides().
    /// </summary>
    private static ResourceDictionary CreateStyleResources() {
        var resources = new ResourceDictionary();

        // Implicit style for FlowDocument (used in RichTextBox)
        var flowDocumentStyle = new Style(typeof(FlowDocument));
        flowDocumentStyle.Setters.Add(new Setter(FlowDocument.FontFamilyProperty, FontFamily()));
        flowDocumentStyle.Setters.Add(new Setter(FlowDocument.FontSizeProperty, (double)TxtSz.normal));
        flowDocumentStyle.Setters.Add(new Setter(FlowDocument.ForegroundProperty, Brushes.Transparent));
        flowDocumentStyle.Setters.Add(new Setter(FlowDocument.PagePaddingProperty, new Thickness(0)));
        resources.Add(typeof(FlowDocument), flowDocumentStyle);

        return resources;
    }

    /// <summary>
    ///     Loads and merges the WpfUiResources dictionary into a FrameworkElement's resources.
    ///     This provides access to implicit styles, typography styles, and theme colors.
    ///     Use this for code-behind controls that need access to the centralized styling.
    /// </summary>
    /// <param name="element">The FrameworkElement to merge resources into</param>
    public static void LoadWpfUiResources(FrameworkElement element) {
        if (element == null) throw new ArgumentNullException(nameof(element));

        var resourceDict = new ResourceDictionary {
            Source = new Uri("pack://application:,,,/PE_Tools;component/addinpalettesuite/core/ui/wpfuiresources.xaml",
                UriKind.Absolute)
        };

        element.Resources.MergedDictionaries.Add(resourceDict);
    }

    /// <summary>
    ///     Initializes the theme manager and applies accent colors.
    ///     Should be called once at application startup or when creating palette windows.
    /// </summary>
    public static void Initialize() {
    }

    /// <summary>
    ///     Applies implicit styles to a Window's resources for automatic control styling.
    ///     Call this when creating windows to ensure all controls get proper styling.
    /// </summary>
    public static void ApplyStylesToWindow(Window window) {
        if (window == null) return;

        // Debug: Log existing styles before adding ours
        Debug.WriteLine("=== Existing Styles in Window Resources ===");
        LogResourceDictionaryStyles(window.Resources);

        var styleResources = CreateStyleResources();

        // Add styles to the beginning of merged dictionaries so they have lower priority 
        // This way WPF.UI's explicit styles take precedence, but our implicit styles provide defaults
        window.Resources.MergedDictionaries.Insert(0, styleResources);

        Debug.WriteLine("=== After Adding ThemeManager Styles ===");
        LogResourceDictionaryStyles(window.Resources);
    }

    /// <summary>
    ///     Debug helper to log all styles in a ResourceDictionary and its merged dictionaries.
    /// </summary>
    private static void LogResourceDictionaryStyles(ResourceDictionary resources, int level = 0) {
        var indent = new string(' ', level * 2);

        // Log implicit styles (keyed by Type)
        foreach (var key in resources.Keys) {
            try {
                // Try to get the value safely
                if (resources.Contains(key)) {
                    var value = resources[key];
                    if (key is Type type && value is Style style)
                        Debug.WriteLine($"{indent}Implicit Style: {type.Name} (Setters: {style.Setters.Count})");
                    else if (key is string stringKey && value is Style namedStyle)
                        Debug.WriteLine($"{indent}Named Style: {stringKey} (Setters: {namedStyle.Setters.Count})");
                }
            } catch {
                // Skip resources that can't be accessed (deferred resources, etc.)
                Debug.WriteLine($"{indent}Resource: {key} (deferred/error)");
            }
        }

        // Recursively log merged dictionaries
        if (resources.MergedDictionaries.Count > 0) {
            Debug.WriteLine($"{indent}Merged Dictionaries: {resources.MergedDictionaries.Count}");
            for (var i = 0; i < resources.MergedDictionaries.Count; i++) {
                try {
                    Debug.WriteLine($"{indent}  [Dictionary {i}] Source: {resources.MergedDictionaries[i].Source}");
                    LogResourceDictionaryStyles(resources.MergedDictionaries[i], level + 2);
                } catch {
                    Debug.WriteLine($"{indent}  [Dictionary {i}] (error accessing)");
                }
            }
        }
    }
}