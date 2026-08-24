using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TrafficStatistics.App.Converters;

/// <summary>
/// Converts a boolean value to a Visibility value. Supports inversion.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolVal)
        {
            var result = Invert ? !boolVal : boolVal;
            return result ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            var result = visibility == Visibility.Visible;
            return Invert ? !result : result;
        }
        return false;
    }
}
