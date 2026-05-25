using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>
    /// Returns Collapsed when the string is null or empty; Visible otherwise.
    /// Used to hide error message TextBlocks when there is no error.
    /// </summary>
    public class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
                   => value is string s && !string.IsNullOrEmpty(s)
                ? System.Windows.Visibility.Visible
       : System.Windows.Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
