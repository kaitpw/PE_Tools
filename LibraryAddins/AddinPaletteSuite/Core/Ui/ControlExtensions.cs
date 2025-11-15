using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Control = System.Windows.Controls.Control;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Extension methods for WPF controls to simplify setting layout properties.
/// </summary>
public static class ControlExtensions {
    public static T WithSpacing<T>(this T border, int leftright, int topbottom) where T : Border {
        var lr = leftright / 2;
        var tb = topbottom / 2;
        if (border.Child is FrameworkElement element)
            element.Margin = new Thickness(lr, tb, lr, tb);
        else if (border.Child is Control control) control.Margin = new Thickness(lr, tb, lr, tb);
        return border;
    }

    public static T WithSpacing<T>(this T border, UiSz leftright = UiSz.m, UiSz topbottom = UiSz.m) where T : Border {
        var lr = (double)leftright / 2;
        var tb = (double)topbottom / 2;
        if (border.Child is FrameworkElement element)
            element.Margin = new Thickness(lr, tb, lr, tb);
        else if (border.Child is Control control) control.Margin = new Thickness(lr, tb, lr, tb);
        return border;
    }

    public static T WithPadding<T>(this T border, UiSz left, UiSz top, UiSz right, UiSz bottom) where T : Border {
        border.Padding = new Thickness((double)left, (double)top, (double)right, (double)bottom);
        return border;
    }

    public static T WithPadding<T>(this T border, UiSz leftright, UiSz topbottom) where T : Border {
        border.Padding = new Thickness((double)leftright, (double)topbottom, (double)leftright, (double)topbottom);
        return border;
    }

    public static T WithMargin<T>(this T border, UiSz margin = UiSz.m) where T : Border {
        border.Margin = new Thickness((double)margin);
        return border;
    }


    public static Border ApplyBorder(this Border border,
        UiSz thickness = UiSz.ss,
        UiSz radius = UiSz.l,
        SolidColorBrush lineColor = null,
        SolidColorBrush bgColor = null) {
        border.BorderThickness = new Thickness((double)thickness);
        border.CornerRadius = new CornerRadius((double)radius);
        border.BorderBrush = lineColor;
        border.Background = bgColor;
        return border;
    }
}