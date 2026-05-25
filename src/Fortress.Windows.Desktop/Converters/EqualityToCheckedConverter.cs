using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>
    /// Two-way converter that maps a string property value to bool (IsChecked on RadioButton).
    /// ConverterParameter is the string value this RadioButton represents.
    /// </summary>
    public class EqualityToCheckedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
   => value is string s && parameter is string p && s == p;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter is string p)
                return p;
            return System.Windows.DependencyProperty.UnsetValue;
        }
    }
}
