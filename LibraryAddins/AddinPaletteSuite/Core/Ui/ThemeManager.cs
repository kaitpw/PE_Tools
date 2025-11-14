using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Centralized theme management for palette UI controls.
///     Wraps ApplicationAccentColorManager and provides type-safe access to colors, typography, and spacing.
/// </summary>
public static class ThemeManager {
    private static bool _isInitialized;

    #region Colors

    // Background Colors (Shadcn-inspired dark palette)
    public static System.Windows.Media.Color BackgroundFillColorPrimary { get; } = System.Windows.Media.Color.FromRgb(0x18, 0x18, 0x1B);
    public static System.Windows.Media.Color BackgroundFillColorSecondary { get; } = System.Windows.Media.Color.FromRgb(0x1F, 0x1F, 0x23);
    public static System.Windows.Media.Color BackgroundFillColorTertiary { get; } = System.Windows.Media.Color.FromRgb(0x27, 0x27, 0x2A);

    // Text Colors
    public static System.Windows.Media.Color TextFillColorPrimary { get; } = System.Windows.Media.Color.FromRgb(0xFA, 0xFA, 0xFA);
    public static System.Windows.Media.Color TextFillColorSecondary { get; } = System.Windows.Media.Color.FromRgb(0xA1, 0xA1, 0xAA);
    public static System.Windows.Media.Color TextFillColorTertiary { get; } = System.Windows.Media.Color.FromRgb(0x71, 0x71, 0x7A);

    // Control Colors
    public static System.Windows.Media.Color ControlFillColorDefault { get; } = System.Windows.Media.Color.FromRgb(0x27, 0x27, 0x2A);
    public static System.Windows.Media.Color ControlStrokeColorDefault { get; } = System.Windows.Media.Color.FromRgb(0x3F, 0x3F, 0x46);

    // Brushes
    public static SolidColorBrush BackgroundFillColorPrimaryBrush { get; } = new SolidColorBrush(BackgroundFillColorPrimary);
    public static SolidColorBrush BackgroundFillColorSecondaryBrush { get; } = new SolidColorBrush(BackgroundFillColorSecondary);
    public static SolidColorBrush BackgroundFillColorTertiaryBrush { get; } = new SolidColorBrush(BackgroundFillColorTertiary);
    public static SolidColorBrush TextFillColorPrimaryBrush { get; } = new SolidColorBrush(TextFillColorPrimary);
    public static SolidColorBrush TextFillColorSecondaryBrush { get; } = new SolidColorBrush(TextFillColorSecondary);
    public static SolidColorBrush TextFillColorTertiaryBrush { get; } = new SolidColorBrush(TextFillColorTertiary);
    public static SolidColorBrush ControlFillColorDefaultBrush { get; } = new SolidColorBrush(ControlFillColorDefault);
    public static SolidColorBrush ControlStrokeColorDefaultBrush { get; } = new SolidColorBrush(ControlStrokeColorDefault);

    #endregion

    #region Typography

    // Font Sizes
    public static double FontSizeSmall { get; } = 8;
    public static double FontSizeNormal { get; } = 10;
    public static double FontSizeMedium { get; } = 12;
    public static double FontSizeLarge { get; } = 14;

    // Font Family
    public static FontFamily FontFamily { get; } = new FontFamily("Segoe UI Variable");

    // Font Weights
    public static FontWeight FontWeightNormal { get; } = FontWeights.Normal;
    public static FontWeight FontWeightMedium { get; } = FontWeights.Medium;
    public static FontWeight FontWeightSemiBold { get; } = FontWeights.SemiBold;

    // Line Heights
    public static double LineHeightSmall { get; } = 14;
    public static double LineHeightNormal { get; } = 16;
    public static double LineHeightLarge { get; } = 18;

    #endregion

    #region Spacing

    // Padding
    public static Thickness PaddingSmall { get; } = new Thickness(6, 2, 6, 2);
    public static Thickness PaddingNormal { get; } = new Thickness(8, 4, 8, 4);
    public static Thickness PaddingMedium { get; } = new Thickness(10, 5, 10, 5);
    public static Thickness PaddingLarge { get; } = new Thickness(12, 6, 12, 6);

    // Item Padding
    public static Thickness ItemPadding { get; } = new Thickness(10, 5, 10, 5);

    // Margins
    public static Thickness MarginSmall { get; } = new Thickness(4, 2, 4, 2);
    public static Thickness MarginNormal { get; } = new Thickness(8, 4, 8, 4);
    public static Thickness MarginMedium { get; } = new Thickness(12, 6, 12, 6);

    #endregion

    #region Borders

    // Border Thickness
    public static Thickness BorderThicknessNone { get; } = new Thickness(0);
    public static Thickness BorderThicknessThin { get; } = new Thickness(1);
    public static Thickness BorderThicknessBottom { get; } = new Thickness(0, 0, 0, 1);
    public static Thickness BorderThicknessTop { get; } = new Thickness(0, 1, 0, 0);

    // Corner Radius
    public static CornerRadius CornerRadiusSmall { get; } = new CornerRadius(4);
    public static CornerRadius CornerRadiusMedium { get; } = new CornerRadius(6);
    public static CornerRadius CornerRadiusLarge { get; } = new CornerRadius(8);
    public static CornerRadius CornerRadiusTop { get; } = new CornerRadius(8, 8, 0, 0);

    #endregion

    #region Icon

    // Icon Properties
    public static double IconSize { get; } = 16;
    public static Thickness IconMargin { get; } = new Thickness(0, 0, 10, 0);
    public static double IconOpacity { get; } = 0.8;

    #endregion

    #region Pill Badge

    // Pill Badge Styling
    public static Thickness PillPadding { get; } = new Thickness(6, 2, 6, 2);
    public static Thickness PillMargin { get; } = new Thickness(8, 0, 0, 0);
    public static CornerRadius PillCornerRadius { get; } = new CornerRadius(4);

    #endregion

    #region Accent Colors (from ApplicationAccentColorManager)

    /// <summary>
    ///     Gets the system accent color from ApplicationAccentColorManager.
    /// </summary>
    public static System.Windows.Media.Color SystemAccent => ApplicationAccentColorManager.SystemAccent;

    /// <summary>
    ///     Gets the system accent brush from ApplicationAccentColorManager.
    /// </summary>
    public static Brush SystemAccentBrush => ApplicationAccentColorManager.SystemAccentBrush;

    /// <summary>
    ///     Gets the primary accent color from ApplicationAccentColorManager.
    /// </summary>
    public static System.Windows.Media.Color PrimaryAccent => ApplicationAccentColorManager.PrimaryAccent;

    /// <summary>
    ///     Gets the primary accent brush from ApplicationAccentColorManager.
    /// </summary>
    public static Brush PrimaryAccentBrush => ApplicationAccentColorManager.PrimaryAccentBrush;

    /// <summary>
    ///     Gets the secondary accent color from ApplicationAccentColorManager.
    /// </summary>
    public static System.Windows.Media.Color SecondaryAccent => ApplicationAccentColorManager.SecondaryAccent;

    /// <summary>
    ///     Gets the secondary accent brush from ApplicationAccentColorManager.
    /// </summary>
    public static Brush SecondaryAccentBrush => ApplicationAccentColorManager.SecondaryAccentBrush;

    /// <summary>
    ///     Gets the tertiary accent color from ApplicationAccentColorManager.
    /// </summary>
    public static System.Windows.Media.Color TertiaryAccent => ApplicationAccentColorManager.TertiaryAccent;

    /// <summary>
    ///     Gets the tertiary accent brush from ApplicationAccentColorManager.
    /// </summary>
    public static Brush TertiaryAccentBrush => ApplicationAccentColorManager.TertiaryAccentBrush;

    #endregion

    #region Initialization

    /// <summary>
    ///     Initializes the theme manager and applies accent colors.
    ///     Should be called once at application startup or when creating palette windows.
    /// </summary>
    public static void Initialize() {
        if (_isInitialized) return;

        // Apply custom accent colors (blue theme)
        ApplicationAccentColorManager.Apply(
            systemAccent: System.Windows.Media.Color.FromArgb(0xFF, 0x00, 0x78, 0xD4),
            primaryAccent: System.Windows.Media.Color.FromArgb(0xFF, 0x00, 0x67, 0xC0),
            secondaryAccent: System.Windows.Media.Color.FromArgb(0xFF, 0x00, 0x3E, 0x92),
            tertiaryAccent: System.Windows.Media.Color.FromArgb(0xFF, 0x00, 0x1A, 0x68)
        );

        _isInitialized = true;
    }

    /// <summary>
    ///     Applies a custom accent color theme.
    /// </summary>
    public static void ApplyAccentColor(System.Windows.Media.Color systemAccent, ApplicationTheme theme = ApplicationTheme.Dark, bool systemGlassColor = false) {
        ApplicationAccentColorManager.Apply(systemAccent, theme, systemGlassColor);
        _isInitialized = true;
    }

    /// <summary>
    ///     Applies system accent colors.
    /// </summary>
    public static void ApplySystemAccent() {
        ApplicationAccentColorManager.ApplySystemAccent();
        _isInitialized = true;
    }

    #endregion
}

