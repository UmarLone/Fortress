using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts a bool to Visibility (true = Visible).</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
         => throw new NotImplementedException();
    }
}
