using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Fortress.Converters
{
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is bool b) return !b;
                // Null or non-bool — treat as false, so inverse = true (show by default)
                return true;
            }
            catch { return BindableProperty.UnsetValue; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is bool b) return !b;
                return BindableProperty.UnsetValue;
            }
            catch { return BindableProperty.UnsetValue; }
        }
    }
}
