using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Control = System.Windows.Controls.Control;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Factory for creating typography styles for WPF controls.
/// </summary>
internal static class TypographyStyleFactory {
    internal static Style CreateCaptionStyle(Type targetType, System.Windows.Media.FontFamily fontFamily) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, fontFamily));
        style.Setters.Add(new Setter(Control.FontSizeProperty, (double)TxtSz.s));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Regular));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 1.2 * (double)TxtSz.s));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }

    internal static Style CreateBodyStyle(Type targetType, System.Windows.Media.FontFamily fontFamily) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, fontFamily));
        style.Setters.Add(new Setter(Control.FontSizeProperty, (double)TxtSz.normal));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Regular));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 1.5 * (double)TxtSz.normal));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }

    internal static Style CreateBodyStrongStyle(Type targetType, System.Windows.Media.FontFamily fontFamily) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, fontFamily));
        style.Setters.Add(new Setter(Control.FontSizeProperty, (double)TxtSz.normal));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 1.5 * (double)TxtSz.normal));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }

    internal static Style CreateSubtitleStyle(Type targetType, System.Windows.Media.FontFamily fontFamily) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, fontFamily));
        style.Setters.Add(new Setter(Control.FontSizeProperty, (double)TxtSz.m));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 1.5 * (double)TxtSz.m));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }

    internal static Style CreateTitleStyle(Type targetType, System.Windows.Media.FontFamily fontFamily) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, fontFamily));
        style.Setters.Add(new Setter(Control.FontSizeProperty, (double)TxtSz.l));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 1.6 * (double)TxtSz.l));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }

    internal static Style CreateTitleLargeStyle(Type targetType, System.Windows.Media.FontFamily fontFamily) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, fontFamily));
        style.Setters.Add(new Setter(Control.FontSizeProperty, (double)TxtSz.ll));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 1.7 * (double)TxtSz.ll));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }

    internal static Style CreateDisplayStyle(Type targetType, System.Windows.Media.FontFamily fontFamily) {
        var style = new Style(targetType);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, fontFamily));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 48.0));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

        if (targetType == typeof(System.Windows.Controls.TextBlock)) {
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineHeightProperty, 64.0));
            style.Setters.Add(new Setter(System.Windows.Controls.TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight));
        }

        return style;
    }
}

