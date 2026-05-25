using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts a bool to Visibility (true = Collapsed — inverse).</summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
