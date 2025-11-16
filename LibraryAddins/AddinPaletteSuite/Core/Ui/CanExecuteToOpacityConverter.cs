using System.Globalization;
using System.Windows.Data;

namespace AddinPaletteSuite.Core.Ui;

/// <summary>
///     Converts CanExecute boolean to opacity value for visual styling
/// </summary>
public class CanExecuteToOpacityConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is bool canExecute) return canExecute ? 1 : ThemeManager.DisabledOpacity;
        return 1;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}