using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using Wpf.Ui.Markup;
using Color = System.Windows.Media.Color;
using Control = System.Windows.Controls.Control;
using MenuItem = Wpf.Ui.Controls.MenuItem;

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
    // Font Family
    public static FontFamily FontFamily() => new("Segoe UI Variable Text");

    /// <summary>
    ///     Gets a typography style by FontTypography enum for type-safe access.
    ///     Uses WPF.UI's FontTypography enum for consistency.
    ///     Creates style for TextBlock by default.
    /// </summary>
    public static Style GetTypographyStyle(FontTypography typography) =>
        GetTypographyStyle(typography, typeof(System.Windows.Controls.TextBlock));

    /// <summary>
    ///     Gets a typography style by FontTypography enum for a specific control type.
    ///     Supports TextBlock, TextBox, and other text-based controls.
    /// </summary>
    public static Style GetTypographyStyle(FontTypography typography, Type targetType) {
        var fontFamily = FontFamily();
        return typography switch {
            FontTypography.Caption => TypographyStyleFactory.CreateCaptionStyle(targetType, fontFamily),
            FontTypography.Body => TypographyStyleFactory.CreateBodyStyle(targetType, fontFamily),
            FontTypography.BodyStrong => TypographyStyleFactory.CreateBodyStrongStyle(targetType, fontFamily),
            FontTypography.Subtitle => TypographyStyleFactory.CreateSubtitleStyle(targetType, fontFamily),
            FontTypography.Title => TypographyStyleFactory.CreateTitleStyle(targetType, fontFamily),
            FontTypography.TitleLarge => TypographyStyleFactory.CreateTitleLargeStyle(targetType, fontFamily),
            FontTypography.Display => TypographyStyleFactory.CreateDisplayStyle(targetType, fontFamily),
            _ => throw new ArgumentOutOfRangeException(nameof(typography), typography, null)
        };
    }

    /// <summary>
    ///     Applies typography style to a control, merging with any existing style.
    ///     Handles type compatibility automatically - works with standard WPF controls and WPF.UI controls.
    /// </summary>
    public static void ApplyTypographyStyle(Control control, FontTypography typography) {
        if (control == null) throw new ArgumentNullException(nameof(control));

        var controlType = control.GetType();
        var typographyStyle = GetTypographyStyle(typography, controlType);

        Style mergedStyle;
        if (control.Style == null) {
            // No existing style, use typography style directly
            mergedStyle = typographyStyle;
        } else {
            // Check if existing style's target type is compatible
            // A style can only be based on a style that targets the same type or a base type
            var existingStyle = control.Style;
            var canUseAsBase = existingStyle.TargetType == controlType ||
                               controlType.IsSubclassOf(existingStyle.TargetType);

            if (canUseAsBase) {
                // Compatible types, can use as base
                mergedStyle = new Style(controlType, existingStyle);
            } else {
                // Incompatible types, create new style without base and copy setters from existing style
                mergedStyle = new Style(controlType);
                foreach (var setter in existingStyle.Setters.OfType<Setter>()) {
                    mergedStyle.Setters.Add(setter);
                }
            }

            // Copy setters from typography style (will override any conflicting setters)
            foreach (var setter in typographyStyle.Setters.OfType<Setter>()) {
                mergedStyle.Setters.Add(setter);
            }
        }

        control.Style = mergedStyle;
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

        // Override default focus visual style to remove outline
        // Source: https://github.com/lepoco/wpfui/blob/main/src/Wpf.Ui/Styles/Controls/FocusVisual.xaml
        var focusVisualStyle = CreateFocusVisualStyle();
        resources.Add("DefaultControlFocusVisualStyle", focusVisualStyle);
        resources.Add(SystemParameters.FocusVisualStyleKey, focusVisualStyle);

        return resources;
    }

    /// <summary>
    ///     Creates a custom FocusVisualStyle with no visible outline.
    ///     Override WPF.UI's default focus rectangle for a cleaner look.
    /// </summary>
    private static Style CreateFocusVisualStyle() {
        var style = new Style();

        // Create a ControlTemplate with a transparent/invisible rectangle
        var template = new ControlTemplate(typeof(Control));
        var rectangleFactory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Rectangle));
        rectangleFactory.SetValue(System.Windows.Shapes.Rectangle.StrokeProperty, Brushes.Transparent);
        rectangleFactory.SetValue(System.Windows.Shapes.Rectangle.StrokeThicknessProperty, 0.0);
        rectangleFactory.SetValue(System.Windows.Shapes.Rectangle.SnapsToDevicePixelsProperty, true);
        template.VisualTree = rectangleFactory;
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
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
    public static double IconOpacity => ThemeSettings.IconOpacity;
    public static CornerRadius Radius => ThemeSettings.Radius;
    public static double DisabledOpacity => ThemeSettings.DisabledOpacity;
}
