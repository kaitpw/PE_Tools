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

    // Font Family
    public static FontFamily FontFamily() => new("Segoe UI Variable Text");

    // Typography Style Keys (for XAML StaticResource)
    public const string CaptionTextBlockStyleKey = "CaptionTextBlockStyle";
    public const string BodyTextBlockStyleKey = "BodyTextBlockStyle";
    public const string BodyStrongTextBlockStyleKey = "BodyStrongTextBlockStyle";
    public const string SubtitleTextBlockStyleKey = "SubtitleTextBlockStyle";
    public const string TitleTextBlockStyleKey = "TitleTextBlockStyle";
    public const string TitleLargeTextBlockStyleKey = "TitleLargeTextBlockStyle";
    public const string DisplayTextBlockStyleKey = "DisplayTextBlockStyle";

    // Typography Style Accessors (for code-behind)
    public static Style CaptionTextBlockStyle() => CreateCaptionStyle();
    public static Style BodyTextBlockStyle() => CreateBodyStyle();
    public static Style BodyStrongTextBlockStyle() => CreateBodyStrongStyle();
    public static Style SubtitleTextBlockStyle() => CreateSubtitleStyle();
    public static Style TitleTextBlockStyle() => CreateTitleStyle();
    public static Style TitleLargeTextBlockStyle() => CreateTitleLargeStyle();
    public static Style DisplayTextBlockStyle() => CreateDisplayStyle();

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
    public static Style GetTypographyStyle(FontTypography typography, Type targetType) => typography switch {
        FontTypography.Caption => CreateCaptionStyle(targetType),
        FontTypography.Body => CreateBodyStyle(targetType),
        FontTypography.BodyStrong => CreateBodyStrongStyle(targetType),
        FontTypography.Subtitle => CreateSubtitleStyle(targetType),
        FontTypography.Title => CreateTitleStyle(targetType),
        FontTypography.TitleLarge => CreateTitleLargeStyle(targetType),
        FontTypography.Display => CreateDisplayStyle(targetType),
        _ => throw new ArgumentOutOfRangeException(nameof(typography), typography, null)
    };

    /// <summary>
    ///     Gets a typography style resource key by FontTypography enum.
    ///     Uses WPF.UI's ToResourceValue() extension method.
    /// </summary>
    public static string GetTypographyResourceKey(FontTypography typography) => typography.ToResourceValue();

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

    // Style factory methods - TextBlock overloads for backward compatibility
    private static Style CreateCaptionStyle() => CreateCaptionStyle(typeof(System.Windows.Controls.TextBlock));
    private static Style CreateBodyStyle() => CreateBodyStyle(typeof(System.Windows.Controls.TextBlock));
    private static Style CreateBodyStrongStyle() => CreateBodyStrongStyle(typeof(System.Windows.Controls.TextBlock));
    private static Style CreateSubtitleStyle() => CreateSubtitleStyle(typeof(System.Windows.Controls.TextBlock));
    private static Style CreateTitleStyle() => CreateTitleStyle(typeof(System.Windows.Controls.TextBlock));
    private static Style CreateTitleLargeStyle() => CreateTitleLargeStyle(typeof(System.Windows.Controls.TextBlock));
    private static Style CreateDisplayStyle() => CreateDisplayStyle(typeof(System.Windows.Controls.TextBlock));

    // Style factory methods - with Type parameter
    private static Style CreateCaptionStyle(Type targetType) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, FontFamily()));
        style.Setters.Add(new Setter(Control.FontSizeProperty, (double)TxtSz.s));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Regular));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 1.2*  (double)TxtSz.s));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }

    private static Style CreateBodyStyle(Type targetType) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, FontFamily()));
        style.Setters.Add(new Setter(Control.FontSizeProperty, (double)TxtSz.normal));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Regular));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 1.5 * (double)TxtSz.normal));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }

    private static Style CreateBodyStrongStyle(Type targetType) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, FontFamily()));
        style.Setters.Add(new Setter(Control.FontSizeProperty, (double)TxtSz.normal));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 1.5 * (double)TxtSz.normal));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }

    private static Style CreateSubtitleStyle(Type targetType) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, FontFamily()));
        style.Setters.Add(new Setter(Control.FontSizeProperty, (double)TxtSz.m));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 1.5 * (double)TxtSz.m));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }

    private static Style CreateTitleStyle(Type targetType) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, FontFamily()));
        style.Setters.Add(new Setter(Control.FontSizeProperty, (double)TxtSz.l));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 1.6 * (double)TxtSz.l));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }

    private static Style CreateTitleLargeStyle(Type targetType) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, FontFamily()));
        style.Setters.Add(new Setter(Control.FontSizeProperty, (double)TxtSz.ll));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 1.7 * (double)TxtSz.ll));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }

    private static Style CreateDisplayStyle(Type targetType) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, FontFamily()));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 48.0));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 64.0));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }

    #endregion


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
    ///     Creates a ResourceDictionary with implicit styles for specific controls.
    ///     Note: TextBlock styles are now handled by CreateTypographyOverrides().
    /// </summary>
    private static ResourceDictionary CreateStyleResources() {
        var resources = new ResourceDictionary();

        // Implicit style for FlowDocument (used in RichTextBox)
        var flowDocumentStyle = new Style(typeof(FlowDocument));
        flowDocumentStyle.Setters.Add(new Setter(FlowDocument.FontFamilyProperty, FontFamily()));
        flowDocumentStyle.Setters.Add(new Setter(FlowDocument.FontSizeProperty, (double)TxtSz.normal));
        flowDocumentStyle.Setters.Add(new Setter(FlowDocument.ForegroundProperty, SecondaryTxt()));
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
    ///     Creates a ResourceDictionary with WPF.UI Color resource overrides for custom theming.
    ///     Sets only base Color resources (not Brushes) - WPF.UI automatically generates corresponding Brush resources.
    ///     Uses ThemeResource enum for type-safe resource keys with IntelliSense support.
    ///     
    ///     Example: Setting TextFillColorPrimary (Color) auto-generates TextFillColorPrimaryBrush (Brush)
    /// </summary>
    public static ResourceDictionary CreateWpfUiResourceOverrides() {
        var resources = new ResourceDictionary {
            // === Application Background ===
            [ThemeResource.ApplicationBackgroundColor.ToString()] = HexColor("#09090b"),

            // === Text Colors ===
            [ThemeResource.TextFillColorPrimary.ToString()] = HexColor("#e2dde1"),
            [ThemeResource.TextFillColorSecondary.ToString()] = HexColor("#aba2a9"),
            [ThemeResource.TextFillColorTertiary.ToString()] = HexColor("#aba2a9"),
            [ThemeResource.TextFillColorDisabled.ToString()] = HexColor("#aba2a9"),
            [ThemeResource.TextPlaceholderColor.ToString()] = HexColor("#aba2a9"),

            // === Control Fills ===
            [ThemeResource.ControlFillColorDefault.ToString()] = HexColor("#3b3e3e"),
            [ThemeResource.ControlFillColorSecondary.ToString()] = HexColor("#343946"),
            [ThemeResource.ControlFillColorTertiary.ToString()] = HexColor("#27272a"),
            [ThemeResource.ControlFillColorDisabled.ToString()] = HexColor("#27272a"),

            // === Control Strong Fills ===
            [ThemeResource.ControlStrongFillColorDefault.ToString()] = HexColor("#343946"),

            // === Solid Backgrounds ===
            [ThemeResource.SolidBackgroundFillColorBase.ToString()] = HexColor("#09090b"),
            [ThemeResource.SolidBackgroundFillColorSecondary.ToString()] = HexColor("#27272a"),
            [ThemeResource.SolidBackgroundFillColorTertiary.ToString()] = HexColor("#27272a"),
            [ThemeResource.SolidBackgroundFillColorQuarternary.ToString()] = HexColor("#3b3e3e"),

            // === Layer Fills ===
            [ThemeResource.LayerFillColorDefault.ToString()] = HexColor("#09090b"),
            [ThemeResource.LayerFillColorAlt.ToString()] = HexColor("#3b3e3e"),

            // === Card Backgrounds ===
            [ThemeResource.CardBackgroundFillColorDefault.ToString()] = HexColor("#3b3e3e"),
            [ThemeResource.CardBackgroundFillColorSecondary.ToString()] = HexColor("#27272a"),

            // === Control Strokes ===
            [ThemeResource.ControlStrokeColorDefault.ToString()] = HexColor("#343946"),
            [ThemeResource.ControlStrokeColorSecondary.ToString()] = HexColor("#27272a"),

            // === Divider ===
            [ThemeResource.DividerStrokeColorDefault.ToString()] = HexColor("#27272a"),

            // Note: WPF.UI automatically creates Brush resources from these Color resources
            // e.g., TextFillColorPrimary → TextFillColorPrimaryBrush
        };

        return resources;
    }

    /// <summary>
    ///     Creates a ResourceDictionary with WPF.UI control dimension and spacing overrides.
    ///     Provides control-level constants for consistent sizing, padding, and corner radii.
    ///     Source: https://github.com/lepoco/wpfui/blob/d6862242cb12cd58b5f95b7dbf26b9b9b158f35f/src/Wpf.Ui/Resources/Variables.xaml
    /// </summary>
    public static ResourceDictionary CreateControlDimensionOverrides() {
        var resources = new ResourceDictionary {
            ["DefaultIconFontSize"] = DefaultIconFontSize,
            ["ControlContentThemeFontSize"] = ControlContentThemeFontSize,
            ["ContentControlFontSize"] = ContentControlFontSize,
            ["ControlCornerRadius"] = Radius,
            ["OverlayCornerRadius"] = Radius,
            ["PopupCornerRadius"] = Radius,
            ["TextControlBorderThemeThickness"] = TextControlBorderThemeThickness,
            ["TextControlBorderThemeThicknessFocused"] = TextControlBorderThemeThicknessFocused,
            ["TextControlThemePadding"] = TextControlThemePadding,
            ["TextControlThemeMinHeight"] = TextControlThemeMinHeight,
            ["TextControlThemeMinWidth"] = TextControlThemeMinWidth,
            ["ListViewItemMinHeight"] = ListViewItemMinHeight,
            ["TreeViewItemMinHeight"] = TreeViewItemMinHeight,
            ["TreeViewItemMultiSelectCheckBoxMinHeight"] = TreeViewItemMultiSelectCheckBoxMinHeight,
            ["TreeViewItemPresenterMargin"] = TreeViewItemPresenterMargin,
            ["TreeViewItemPresenterPadding"] = TreeViewItemPresenterPadding,
            ["TimePickerHostPadding"] = TimePickerHostPadding,
            ["DatePickerHostPadding"] = DatePickerHostPadding,
            ["DatePickerHostMonthPadding"] = DatePickerHostMonthPadding,
            ["ComboBoxEditableTextPadding"] = ComboBoxEditableTextPadding,
            ["ComboBoxMinHeight"] = ComboBoxMinHeight,
            ["ComboBoxPadding"] = ComboBoxPadding,
            ["NavigationViewItemOnLeftMinHeight"] = NavigationViewItemOnLeftMinHeight
        };

        return resources;
    }

    /// <summary>
    ///     Creates a ResourceDictionary with WPF.UI Typography style overrides.
    ///     Overrides the default WPF.UI TextBlock styles to customize typography throughout the application.
    ///     Uses WPF.UI's FontTypography enum for type-safe resource key generation.
    /// </summary>
    public static ResourceDictionary CreateTypographyOverrides() {
        var resources = new ResourceDictionary();

        // Base TextBlock style (implicit) - applies to all TextBlocks without explicit style
        var baseTextBlockStyle = new Style(typeof(System.Windows.Controls.TextBlock));
        baseTextBlockStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontFamilyProperty, FontFamily()));
        baseTextBlockStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontSizeProperty, 10.0));
        baseTextBlockStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 14.0));
        baseTextBlockStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Regular));
        baseTextBlockStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        resources.Add(typeof(System.Windows.Controls.TextBlock), baseTextBlockStyle);

        // Add named typography styles using WPF.UI's FontTypography enum for resource keys
        resources.Add(FontTypography.Caption.ToResourceValue(), CreateCaptionStyle());
        resources.Add(FontTypography.Body.ToResourceValue(), CreateBodyStyle());
        resources.Add(FontTypography.BodyStrong.ToResourceValue(), CreateBodyStrongStyle());
        resources.Add(FontTypography.Subtitle.ToResourceValue(), CreateSubtitleStyle());
        resources.Add(FontTypography.Title.ToResourceValue(), CreateTitleStyle());
        resources.Add(FontTypography.TitleLarge.ToResourceValue(), CreateTitleLargeStyle());
        resources.Add(FontTypography.Display.ToResourceValue(), CreateDisplayStyle());

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


    #region Control Dimensions & Spacing

    // Icon Properties
    public static double IconSize { get; } = 16;
    public static Thickness IconMargin { get; } = new(0, 0, 10, 0);
    public static double IconOpacity { get; } = 0.8;

    // Default Icon Font Size
    public static double DefaultIconFontSize { get; } = 16;

    // Control Font Sizes
    public static double ControlContentThemeFontSize { get; } = (double)TxtSz.normal;
    public static double ContentControlFontSize { get; } = (double)TxtSz.normal;

    // Corner Radii
    public static CornerRadius ControlCornerRadius { get; } = new(4);
    public static CornerRadius OverlayCornerRadius { get; } = new(4);
    public static CornerRadius Radius { get; } = new(6);

    // Text Control Dimensions
    public static Thickness TextControlBorderThemeThickness { get; } = new(1);
    public static Thickness TextControlBorderThemeThicknessFocused { get; } = new(2);
    public static Thickness TextControlThemePadding { get; } = new(10, 8, 10, 7);
    public static double TextControlThemeMinHeight { get; } = 24;
    public static double TextControlThemeMinWidth { get; } = 0;

    // List & Tree View Item Heights
    public static double ListViewItemMinHeight { get; } = 32;
    public static double TreeViewItemMinHeight { get; } = 24;
    public static double TreeViewItemMultiSelectCheckBoxMinHeight { get; } = 24;
    public static double TreeViewItemPresenterMargin { get; } = 0;
    public static double TreeViewItemPresenterPadding { get; } = 0;

    // Picker Paddings
    public static Thickness TimePickerHostPadding { get; } = new(0, 1, 0, 2);
    public static Thickness DatePickerHostPadding { get; } = new(0, 1, 0, 2);
    public static Thickness DatePickerHostMonthPadding { get; } = new(9, 0, 0, 1);
    public static Thickness ComboBoxEditableTextPadding { get; } = new(10, 0, 30, 0);

    // ComboBox Dimensions
    public static double ComboBoxMinHeight { get; } = 24;
    public static Thickness ComboBoxPadding { get; } = new(12, 1, 0, 3);

    // Navigation View
    public static double NavigationViewItemOnLeftMinHeight { get; } = 32;

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