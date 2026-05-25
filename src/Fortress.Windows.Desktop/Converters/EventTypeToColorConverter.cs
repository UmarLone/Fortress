using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts an event type int to a colour.</summary>
    public class EventTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not int t) return new SolidColorBrush(Colors.Gray);
            var hex = t switch
            {
                3 => "#EF4444",
                5 => "#22C55E",
                6 => "#F59E0B",
                _ => "#3B82F6"
            };
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
