#nullable enable

using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;
using Color = System.Windows.Media.Color;

namespace AddinPaletteSuite.Core.Ui;

public enum UiSz {
    none = 0,
    ss = 1,
    s = 2,
    m = 4,
    l = 6,
    ll = 9
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
    private static bool _resourcesLogged;

    public static double IconOpacity => ThemeSettings.IconOpacity;
    public static CornerRadius Radius => ThemeSettings.Radius;

    public static double DisabledOpacity => ThemeSettings.DisabledOpacity;

    // Font Family
    public static FontFamily FontFamily() => new("Segoe UI Variable Text");

    /// <summary>
    ///     Gets a WPF.UI theme brush from the Application's resource dictionary.
    /// </summary>
    /// <param name="themeResource">The theme resource enum value</param>
    /// <returns>The brush from the current theme</returns>
    public static Brush GetThemeBrush(ThemeResource themeResource) {
        if (themeResource == ThemeResource.Unknown) return Brushes.Transparent;

        // Log available resources once for debugging
        if (!_resourcesLogged) {
            _resourcesLogged = true;
            LogAvailableThemeResources();
        }

        // Get the resource key string from the enum
        var resourceKey = themeResource.ToString();
        Debug.WriteLine($"[ThemeManager] Looking for resource: '{resourceKey}'");

        // Try to find the resource in Application resources
        if (Application.Current?.TryFindResource(resourceKey) is Brush brush) {
            Debug.WriteLine($"[ThemeManager] Found brush for '{resourceKey}': {brush}");
            return brush;
        }

        Debug.WriteLine($"[ThemeManager] Resource '{resourceKey}' NOT FOUND in Application.Current.TryFindResource");

        // WPF.UI resources aren't always directly accessible via TryFindResource
        // They're resolved dynamically through DynamicResource bindings
        // For now, return a sensible fallback based on the resource type
        return Brushes.Red;
    }

    private static Brush GetFallbackBrush(ThemeResource themeResource) =>
        // Provide reasonable fallbacks for common theme resources
        themeResource.ToString() switch {
            var s when s.Contains("Background") => new SolidColorBrush(
                Color.FromRgb(32, 32, 32)), // Dark background
            var s when s.Contains("Foreground") || s.Contains("Text") => new SolidColorBrush(
                Color.FromRgb(255, 255, 255)), // White text
            var s when s.Contains("Border") || s.Contains("Stroke") => new SolidColorBrush(
                Color.FromRgb(60, 60, 60)), // Subtle border
            _ => Brushes.Transparent
        };

    /// <summary>
    ///     Gets a WPF.UI theme brush from the Application's resource dictionary by string key.
    /// </summary>
    /// <param name="resourceKey">The resource key string (e.g., "ApplicationBackgroundBrush")</param>
    /// <returns>The brush from the current theme</returns>
    public static Brush GetThemeBrush(string resourceKey) {
        if (string.IsNullOrEmpty(resourceKey)) return Brushes.Transparent;

        // Try to find the resource in Application resources
        if (Application.Current?.TryFindResource(resourceKey) is Brush brush) return brush;

        return Brushes.Red;
    }

    /// <summary>
    ///     Gets the default application background brush.
    /// </summary>
    public static Brush ApplicationBackground() =>
        GetThemeBrush(ThemeResource.ApplicationBackgroundBrush);

    /// <summary>
    ///     Gets the primary text foreground brush.
    /// </summary>
    public static Brush TextFillColorPrimaryBrush() =>
        GetThemeBrush(ThemeResource.TextFillColorPrimaryBrush);

    /// <summary>
    ///     Gets a typography style by FontTypography enum.
    ///     Loads the style from XAML resources defined in TypographyOverrides.xaml.
    ///     The targetType parameter is kept for API compatibility but styles are TextBlock-based.
    /// </summary>
    /// <param name="typography">The FontTypography level to get</param>
    /// <param name="_">Unused type parameter for API compatibility</param>
    /// <param name="searchContext">Optional element to search for resources in its resource chain before Application</param>
    public static Style GetTypographyStyle(
        FontTypography typography,
        FrameworkElement? searchContext = null
    ) {
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
    ///     Debug helper to log available brush resources in Application.
    /// </summary>
    public static void LogAvailableThemeResources() {
        if (Application.Current == null) {
            Debug.WriteLine("[ThemeManager] Application.Current is null!");
            return;
        }

        Debug.WriteLine("[ThemeManager] ===== Available Brush Resources =====");
        LogBrushesInDictionary(Application.Current.Resources, 0);
    }

    private static void LogBrushesInDictionary(ResourceDictionary resources, int level) {
        var indent = new string(' ', level * 2);

        // Log brush resources (keyed by string)
        foreach (var key in resources.Keys) {
            try {
                if (key is string stringKey && resources.Contains(stringKey)) {
                    var value = resources[stringKey];
                    if (value is Brush) Debug.WriteLine($"{indent}Brush: {stringKey}");
                }
            } catch {
                // Skip resources that can't be accessed
            }
        }

        // Recursively log merged dictionaries
        foreach (var mergedDict in resources.MergedDictionaries) LogBrushesInDictionary(mergedDict, level + 1);
    }

    /// <summary>
    ///     Debug helper to log all styles in a ResourceDictionary and its merged dictionaries.
    ///     Keep for later debugging
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