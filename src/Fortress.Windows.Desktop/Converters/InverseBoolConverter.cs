using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Inverts a bool — returns true when input is false. Used for IsEnabled bindings.</summary>
    public class InverseBoolConverter : IValueConverter
 {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : true;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
 => value is bool b ? !b : false;
    }
}
