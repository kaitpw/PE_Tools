using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Color = System.Windows.Media.Color;
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
///     Centralized theme management for palette UI controls.
///     Wraps ApplicationAccentColorManager and provides type-safe access to colors, typography, and spacing.
/// </summary>
public static class ThemeManager {
    // Helper method to create SolidColorBrush from hex string
    private static SolidColorBrush Hex(string hex, double opacity = 1.0) {
        hex = hex.TrimStart('#');
        var r = Convert.ToByte(hex[..2], 16);
        var g = Convert.ToByte(hex[2..4], 16);
        var b = Convert.ToByte(hex[4..6], 16);
        return new SolidColorBrush(Color.FromRgb(r, g, b)) { Opacity = opacity };
    }

    // Helper method to create Color from hex string (for WPF.UI theme manager)
    private static Color HexColor(string hex) {
        hex = hex.TrimStart('#');
        var r = Convert.ToByte(hex[..2], 16);
        var g = Convert.ToByte(hex[2..4], 16);
        var b = Convert.ToByte(hex[4..6], 16);
        return Color.FromRgb(r, g, b);
    }

    // Shadcn-inspired dark theme with lavender-whitish-blue accents
    // Background colors (zinc scale from shadcn)
    public static SolidColorBrush PrimaryBg() => Hex("#09090b"); // zinc-950 - darkest background
    public static SolidColorBrush SecondaryBg() => Hex("#3b3e3e"); // zinc-900 - elevated surfaces
    public static SolidColorBrush TertiaryBg() => Hex("#27272a"); // zinc-800 - hover states

    // Accent/Highlight colors (lavender-blue spectrum)
    public static SolidColorBrush PrimaryHi() => Hex("#fca4fc", 0.5); // primary accent at 50% opacity
    public static SolidColorBrush SecondaryHi() => Hex("#343946"); // lighter accent/hover at 50% opacity

    // Text colors
    public static SolidColorBrush PrimaryTxt() => Hex("#e2dde1"); // zinc-50 - primary text
    public static SolidColorBrush SecondaryTxt() => Hex("#aba2a9"); // zinc-400 - muted text

    #region Typography

    // Font Sizes


    // Font Family
    public static FontFamily FontFamily() => new("Segoe UI Variable Text");

    // public static ApplyFont(TextBlock)

    #endregion


    /// <summary>
    ///     Initializes the theme manager and applies accent colors.
    ///     Should be called once at application startup or when creating palette windows.
    /// </summary>
    public static void Initialize() {
        ApplicationThemeManager.Apply(
            ApplicationTheme.Dark, WindowBackdropType.Tabbed
        );
        ApplicationAccentColorManager.Apply(
            HexColor("#09090b"),
            HexColor("#18181b"),
            HexColor("#27272a"),
            HexColor("#09090b")
        );
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
            if (key is Type type && resources[key] is Style style)
                Debug.WriteLine($"{indent}Implicit Style: {type.Name} (Setters: {style.Setters.Count})");
            else if (key is string stringKey && resources[key] is Style namedStyle)
                Debug.WriteLine($"{indent}Named Style: {stringKey} (Setters: {namedStyle.Setters.Count})");
        }

        // Recursively log merged dictionaries
        if (resources.MergedDictionaries.Count > 0) {
            Debug.WriteLine($"{indent}Merged Dictionaries: {resources.MergedDictionaries.Count}");
            for (var i = 0; i < resources.MergedDictionaries.Count; i++) {
                Debug.WriteLine($"{indent}  [Dictionary {i}] Source: {resources.MergedDictionaries[i].Source}");
                LogResourceDictionaryStyles(resources.MergedDictionaries[i], level + 2);
            }
        }
    }

    /// <summary>
    ///     Applies standard font styling to a WPF.UI TextBlock control.
    ///     Use this helper to avoid manual property setting in code-behind.
    /// </summary>
    public static T StyleTextBlock<T>(T textBlock, TxtSz fontSize = TxtSz.normal, bool isPrimary = true)
        where T : TextBlock {
        textBlock.FontFamily = FontFamily();
        textBlock.FontSize = (double)fontSize;
        textBlock.Foreground = isPrimary ? PrimaryTxt() : SecondaryTxt();
        textBlock.Padding = new Thickness(0);
        textBlock.Margin = new Thickness(0);

        // Set LineHeight to match font size to prevent extra spacing
        textBlock.LineHeight = 1.3 * (double)fontSize;
        textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;

        // Debug: Log the actual computed values
        Debug.WriteLine(
            $"StyleTextBlock - FontSize: {textBlock.FontSize}, Padding: {textBlock.Padding}, Margin: {textBlock.Margin}, LineHeight: {textBlock.LineHeight}");

        return textBlock;
    }

    /// <summary>
    ///     Applies standard font styling to a standard TextBlock control.
    ///     Use this helper to avoid manual property setting in code-behind.
    /// </summary>
    public static System.Windows.Controls.TextBlock StyleTextBlock(System.Windows.Controls.TextBlock textBlock,
        TxtSz fontSize = TxtSz.normal,
        bool isPrimary = true) {
        textBlock.FontFamily = FontFamily();
        textBlock.FontSize = (double)fontSize;
        textBlock.Foreground = isPrimary ? PrimaryTxt() : SecondaryTxt();
        textBlock.Padding = new Thickness(0);
        textBlock.Margin = new Thickness(0);

        // Set LineHeight to match font size to prevent extra spacing
        textBlock.LineHeight = (double)fontSize;
        textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;

        // Debug: Log the actual computed values
        Debug.WriteLine(
            $"StyleTextBlock (std) - FontSize: {textBlock.FontSize}, Padding: {textBlock.Padding}, Margin: {textBlock.Margin}, LineHeight: {textBlock.LineHeight}");

        return textBlock;
    }

    /// <summary>
    ///     Applies standard font styling to a MenuItem control.
    ///     Use this helper to avoid manual property setting in code-behind.
    /// </summary>
    public static MenuItem StyleMenuItem(MenuItem menuItem) {
        menuItem.FontFamily = FontFamily();
        menuItem.FontSize = (double)TxtSz.normal;
        menuItem.FontWeight = FontWeights.SemiBold;
        // menuItem.MaxHeight = 30; // this is problematic because its cuts off the text in the item. but the menu items are too big
        menuItem.Foreground = PrimaryTxt();
        menuItem.Padding = new Thickness(0); // this also doesn't do anything
        return menuItem;
    }

    /// <summary>
    ///     Applies implicit styles to Application resources for automatic control styling.
    ///     Can be called at initialization or to update theme at runtime.
    ///     Note: In Revit add-ins, Application.Current may be null, prefer ApplyStylesToWindow instead.
    /// </summary>
    private static void ApplyImplicitStyles() {
        if (Application.Current == null) return;

        var styleResources = CreateStyleResources();

        // Remove old ThemeManager styles if they exist
        var existingThemeDict = Application.Current.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.ToString().Contains("ThemeManagerStyles") ?? false);
        if (existingThemeDict != null) _ = Application.Current.Resources.MergedDictionaries.Remove(existingThemeDict);

        // Add new styles
        Application.Current.Resources.MergedDictionaries.Add(styleResources);
    }

    /// <summary>
    ///     Updates the theme at runtime by regenerating and reapplying implicit styles.
    ///     Call this after changing theme colors to update all controls without recreating windows.
    ///     Note: This only works if styles were applied via ApplyImplicitStyles (Application.Current).
    ///     For window-specific styles, call ApplyStylesToWindow again on each window.
    /// </summary>
    public static void UpdateTheme() => ApplyImplicitStyles();

    /// <summary>
    ///     Creates a ResourceDictionary with implicit styles for WPF.UI controls.
    ///     These styles automatically apply ThemeManager values to all controls without manual code-behind styling.
    /// </summary>
    private static ResourceDictionary CreateStyleResources() {
        var resources = new ResourceDictionary();

        // Implicit style for standard TextBlock (fallback for non-WPF.UI controls)
        var textBlockStyle = new Style(typeof(System.Windows.Controls.TextBlock));
        textBlockStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontFamilyProperty, FontFamily()));
        textBlockStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontSizeProperty,
            (double)TxtSz.normal));
        textBlockStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.ForegroundProperty, PrimaryTxt()));
        textBlockStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.TextTrimmingProperty,
            TextTrimmingMode));
        resources.Add(typeof(System.Windows.Controls.TextBlock), textBlockStyle);

        // Note: We cannot add implicit styles for Wpf.Ui.Controls.TextBlock or MenuItem here
        // because WPF.UI already has implicit styles for them in the merged resource dictionaries.
        // Attempting to add them will cause "Item has already been added" exception.
        // Instead, controls must explicitly set font properties in code-behind when needed.

        // Implicit style for FlowDocument (used in RichTextBox)
        var flowDocumentStyle = new Style(typeof(FlowDocument));
        flowDocumentStyle.Setters.Add(new Setter(FlowDocument.FontFamilyProperty, FontFamily()));
        flowDocumentStyle.Setters.Add(new Setter(FlowDocument.FontSizeProperty, (double)TxtSz.normal));
        flowDocumentStyle.Setters.Add(new Setter(FlowDocument.ForegroundProperty, SecondaryTxt()));
        flowDocumentStyle.Setters.Add(new Setter(FlowDocument.PagePaddingProperty, new Thickness(0)));
        resources.Add(typeof(FlowDocument), flowDocumentStyle);

        return resources;
    }


    /// <summary>
    ///     Applies a custom accent color theme.
    /// </summary>
    public static void ApplyAccentColor(Color systemAccent,
        ApplicationTheme theme = ApplicationTheme.Dark,
        bool systemGlassColor = false) => ApplicationAccentColorManager.Apply(systemAccent, theme, systemGlassColor);

    /// <summary>
    ///     Applies system accent colors.
    /// </summary>
    public static void ApplySystemAccent() => ApplicationAccentColorManager.ApplySystemAccent();


    #region Icon

    // Icon Properties
    public static double IconSize { get; } = 16;
    public static Thickness IconMargin { get; } = new(0, 0, 10, 0);
    public static double IconOpacity { get; } = 0.8;

    #endregion


    #region Tooltip

    // Tooltip Styling
    public static double TooltipWidth { get; } = 250.0;
    public static double TooltipMinHeight { get; } = 100.0;
    public static Thickness TooltipBorderPadding { get; } = new(8, 6, 8, 6);

    #endregion


    #region ListBox Item States

    // Opacity Values
    public static double ItemOpacityEnabled { get; } = 1.0;
    public static double ItemOpacityDisabled { get; } = 0.4;

    // Text Trimming
    public static TextTrimming TextTrimmingMode { get; } = TextTrimming.CharacterEllipsis;

    #endregion
}