using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AddinCmdPalette.Core;

/// <summary> Coerce value to a display state </summary>
public class VisibilityConverter : IValueConverter {
    public static readonly VisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch {
            bool boolValue => boolValue
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed,
            int intValue => intValue > 0
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed,
            string stringValue => !string.IsNullOrWhiteSpace(stringValue)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed,
            System.Windows.Media.Imaging.BitmapImage img => img != null
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed,
            _ => System.Windows.Visibility.Collapsed
        };

    public object ConvertBack(object _, Type __, object ___, CultureInfo ____) =>
        throw new NotImplementedException();
}

