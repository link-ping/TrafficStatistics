using System;
using System.Globalization;
using System.Windows.Data;

namespace TrafficStatistics.App.Converters;

/// <summary>
/// Converter to check if selected nav index matches parameters for RadioButton checking.
/// </summary>
public class NavigationIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int currentIndex && parameter is string paramStr && int.TryParse(paramStr, out int targetIndex))
        {
            return currentIndex == targetIndex;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr && int.TryParse(paramStr, out int targetIndex))
        {
            return targetIndex;
        }
        return Binding.DoNothing;
    }
}
