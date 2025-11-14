using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Color = System.Windows.Media.Color;

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
    #region Typography

    // Font Sizes


    // Font Family
    public static FontFamily FontFamily { get; } = new("Segoe UI Variable");

    #endregion

    public static SolidColorBrush PrimaryAccent => new(PrimaryColor);
    public static SolidColorBrush SecondaryAccent => new(SecondaryColor);
    public static SolidColorBrush TertiaryAccent => new(TertiaryColor);
    public static SolidColorBrush SystemAccent => new(SystemColor);
    public static SolidColorBrush SecondaryTextAccent => new(TextSecondaryColor);
    public static SolidColorBrush PrimaryTextAccent => new(TextPrimaryColor);


    /// <summary>
    ///     Initializes the theme manager and applies accent colors.
    ///     Should be called once at application startup or when creating palette windows.
    /// </summary>
    public static void Initialize() {
        ApplicationThemeManager.Apply(
            ApplicationTheme.Dark, WindowBackdropType.Tabbed
        );
        // Apply custom accent colors (zinc/neutral theme matching our existing palette)
        ApplicationAccentColorManager.Apply(
            SystemColor, // zinc-700 - primary accent
            PrimaryColor, // zinc-800 - secondary
            SecondaryColor, // zinc-900 - tertiary
            TertiaryColor // zinc-950 - quaternary
        );
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


    #region Colors

    // Background Colors (Shadcn-inspired dark palette - zinc scale)
    public static Color TertiaryColor { get; } = Color.FromRgb(0x18, 0x18, 0x1B); // zinc-950

    public static Color SecondaryColor { get; } = Color.FromRgb(0x1F, 0x1F, 0x23); // zinc-900

    // Control Colors (Shadcn-inspired borders and fills)
    public static Color PrimaryColor { get; } = Color.FromRgb(0x27, 0x27, 0x2A); // zinc-800
    public static Color SystemColor { get; } = Color.FromRgb(0x3F, 0x3F, 0x46); // zinc-700

    // Text Colors
    public static Color TextPrimaryColor { get; } = Color.FromRgb(0xFA, 0xFA, 0xFA); // #FAFAFA
    public static Color TextSecondaryColor { get; } = Color.FromRgb(0xA1, 0xA1, 0xAA); // #A1A1AA (zinc-400)

    #endregion


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

    // Corner Radius
    public static CornerRadius ItemCornerRadius { get; } = new CornerRadius((double)UiSz.s);

    #endregion
}